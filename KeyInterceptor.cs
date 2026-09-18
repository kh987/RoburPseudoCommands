using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Topomatic.Cad.View;

namespace RoburPseudoCommands
{
    internal sealed class KeyInterceptor : IMessageFilter
    {
        private const int WmKeyDown = 0x0100;
        private const int WmKeyUp = 0x0101;
        private const int WmSysKeyDown = 0x0104;
        private const int WmLButtonUp = 0x0202;
        private const int VkReturn = 0x0d;
        private const int VkSpace = 0x20;
        private const uint MapVkToVsc = 0;

        private static readonly object SyncRoot = new object();
        private static KeyInterceptor _instance;

        private readonly Func<CadView> _getCadView;
        private QuickInputForm _popup;
        private EmergencyCommandForm _emergencyPalette;
        private bool _popupOpenPending;
        private bool _emergencyDispatchPending;
        private bool _repeatDispatchPending;
        private bool _emergencyMode;
        private bool _emergencyNoticeShown;
        private string _lastPopupAlias;
        private int _consecutiveErrors;
        private long _nextDispatchId;
        private DateTime _lastBlockedQuickInputLogUtc = DateTime.MinValue;

        private KeyInterceptor(Func<CadView> getCadView)
        {
            _getCadView = getCadView;
        }

        public static bool IsAttached
        {
            get { lock (SyncRoot) return _instance != null; }
        }

        public static bool IsEmergencyMode
        {
            get { lock (SyncRoot) return _instance != null && _instance._emergencyMode; }
        }

        public static void Attach(Func<CadView> getCadView)
        {
            if (getCadView == null) throw new ArgumentNullException("getCadView");
            lock (SyncRoot)
            {
                if (_instance != null) return;
                var instance = new KeyInterceptor(getCadView);
                Application.AddMessageFilter(instance);
                _instance = instance;
                Logger.Info("input message filter attached; Space acts as Enter; safeEditRoutingEnabled=" +
                    PluginSettings.IsSafeDeleteUndoEnabled() +
                    "; protectedCommands=erase,undo,copyclip,copybase,pasteclip,dsettings,point_sign_library," +
                    "linear_sign_library,area_sign_library,models_library,smt_manager,smdx_manager," +
                    "materials_settings_manager,options,toolbar_settings; emergency hotkey=Ctrl+Shift+F12");
            }
        }

        public static void NotifyCommandFailure(Exception exception)
        {
            if (!EmergencyCommandRegistry.IsRegistryFailure(exception)) return;
            EnterEmergencyMode("registry exception: " + exception.GetType().Name, true);
        }

        public static bool EnterEmergencyMode(string reason, bool showNotice)
        {
            KeyInterceptor instance;
            lock (SyncRoot) instance = _instance;
            if (instance == null) return false;
            instance.ActivateEmergencyMode(reason, showNotice);
            return true;
        }

        public static void ClearPopupRepeatHistory(string reason)
        {
            KeyInterceptor instance;
            lock (SyncRoot) instance = _instance;
            if (instance == null) return;
            instance.ClearPopupRepeatHistoryCore(reason);
        }

        public bool PreFilterMessage(ref Message message)
        {
            try
            {
                if (message.Msg == WmLButtonUp &&
                    (_emergencyMode || PluginSettings.IsSafeDeleteUndoEnabled()))
                {
                    var menuHandled = TryHandleProtectedMenuClick(message);
                    _consecutiveErrors = 0;
                    return menuHandled;
                }

                if (message.Msg != WmKeyDown && message.Msg != WmSysKeyDown) return false;
                var handled = HandleKeyDown(message);
                _consecutiveErrors = 0;
                return handled;
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                Logger.Error("keyboard message filter failed consecutiveErrors=" + _consecutiveErrors, ex);
                if (_consecutiveErrors >= 3) DetachAfterErrors();
                return false;
            }
        }

        private bool HandleKeyDown(Message message)
        {
            var virtualKey = message.WParam.ToInt32();
            var isRepeated = IsRepeatedKeyDown(message.LParam);

            if (virtualKey == (int)Keys.F12 &&
                (Control.ModifierKeys & (Keys.Control | Keys.Shift | Keys.Alt)) == (Keys.Control | Keys.Shift))
            {
                ActivateEmergencyMode("manual hotkey", false);
                ScheduleEmergencyPalette();
                return true;
            }

            if (_popup != null) return false;
            if (_popupOpenPending) return true;

            if ((_emergencyMode || PluginSettings.IsSafeDeleteUndoEnabled()) &&
                TryHandleProtectedEditKey(virtualKey, isRepeated))
                return true;

            if ((Control.ModifierKeys & (Keys.Control | Keys.Alt | Keys.Shift)) != Keys.None) return false;

            CadView cadView;
            if (!TryGetFocusedCadView(out cadView)) return false;

            if (virtualKey == VkReturn ||
                (virtualKey == VkSpace && PluginSettings.IsSpaceActsAsEnterEnabled()))
                return HandleRepeatKey(cadView, virtualKey, isRepeated);

            if (isRepeated) return false;
            if (!IsPotentialQuickInputKey(virtualKey)) return false;
            if (!PluginSettings.IsQuickInputEnabled()) return false;

            string firstCharacter;
            if (!KeyboardInputTranslator.TryGetQuickInputCharacter(virtualKey, message.LParam, out firstCharacter)) return false;

            string blockReason;
            if (TryGetQuickInputBlockReason(cadView, out blockReason))
            {
                LogQuickInputState("blocked", cadView, firstCharacter, true, blockReason);
                return false;
            }

            LogQuickInputState("opening", cadView, firstCharacter, false, string.Empty);
            ScheduleQuickInput(cadView, firstCharacter);
            return true;
        }

        private bool HandleRepeatKey(CadView cadView, int virtualKey, bool isRepeated)
        {
            var keyName = virtualKey == VkReturn ? "Enter" : "Space";
            if (isRepeated || _repeatDispatchPending)
            {
                Logger.Info("repeat suppressed key=" + keyName + " reason=" +
                    (isRepeated ? "held-key" : "dispatch-pending"));
                return true;
            }

            if (cadView.IsGettingValue)
            {
                if (PluginSettings.IsLogEnabled())
                    Logger.Info(keyName + "->Enter isGettingValue=True lastUserCmd='" +
                        SanitizeLogValue(cadView.LastUserCmd) + "' emergencyMode=" + _emergencyMode);
                return virtualKey == VkSpace ? PostEnter(GetFocus()) : false;
            }

            if (cadView.IsModalEdit || CadView.ActionStackCount != 0)
            {
                Logger.Info("repeat unavailable key=" + keyName + " reason=cad-view-busy");
                return virtualKey == VkSpace ? PostEnter(GetFocus()) : false;
            }

            if (!PluginSettings.IsQuickInputEnabled())
            {
                Logger.Info("repeat unavailable key=" + keyName + " reason=quick-input-disabled");
                return virtualKey == VkSpace ? PostEnter(GetFocus()) : false;
            }

            var alias = _lastPopupAlias;
            if (!string.IsNullOrEmpty(alias))
            {
                ScheduleAliasExecution(cadView, alias, "repeat-" + keyName.ToLowerInvariant(), false);
                return true;
            }

            Logger.Info("repeat unavailable key=" + keyName + " reason=no-popup-history");
            return virtualKey == VkSpace ? PostEnter(GetFocus()) : false;
        }

        private bool TryHandleProtectedEditKey(int virtualKey, bool isRepeated)
        {
            var modifiers = Control.ModifierKeys & (Keys.Control | Keys.Alt | Keys.Shift);
            string command = null;
            if (virtualKey == (int)Keys.Delete && modifiers == Keys.None)
                command = "erase";
            else if (virtualKey == (int)Keys.Z && modifiers == Keys.Control)
                command = "undo";
            else if (virtualKey == (int)Keys.C && modifiers == Keys.Control)
                command = "copyclip";
            else if (virtualKey == (int)Keys.C && modifiers == (Keys.Control | Keys.Shift))
                command = "copybase";
            else if (virtualKey == (int)Keys.V && modifiers == Keys.Control)
                command = "pasteclip";

            if (command == null) return false;

            CadView cadView;
            if (!TryGetFocusedCadView(out cadView) || cadView.IsGettingValue || cadView.IsModalEdit || CadView.ActionStackCount != 0)
                return false;

            if (isRepeated || _emergencyDispatchPending) return true;

            ScheduleEmergencyCommand(cadView, command,
                _emergencyMode ? "keyboard-emergency" : "keyboard-safe-snapshot");
            return true;
        }

        private bool TryHandleProtectedMenuClick(Message message)
        {
            var strip = Control.FromHandle(message.HWnd) as ToolStrip;
            if (strip == null) return false;

            var item = strip.GetItemAt(strip.PointToClient(Cursor.Position));
            string command;
            if (!TryGetProtectedMenuCommand(item, out command)) return false;

            if (_emergencyDispatchPending)
            {
                Logger.Info("protected menu click suppressed command='" + command + "' reason=dispatch-pending");
                return true;
            }

            CadView cadView;
            try { cadView = _getCadView(); }
            catch (Exception ex)
            {
                Logger.Error("protected menu click failed to resolve CadView command='" + command + "'", ex);
                return true;
            }

            if (cadView == null || cadView.IsDisposed || !cadView.IsHandleCreated ||
                cadView.IsGettingValue || cadView.IsModalEdit || CadView.ActionStackCount != 0)
            {
                Logger.Info("protected menu click suppressed command='" + command + "' reason=cad-view-busy-or-unavailable");
                return true;
            }

            var dropDown = strip as ToolStripDropDown;
            if (dropDown != null)
                dropDown.Close(ToolStripDropDownCloseReason.ItemClicked);

            ScheduleEmergencyCommand(cadView, command,
                _emergencyMode ? "menu-emergency" : "menu-safe-snapshot");
            return true;
        }

        private static bool TryGetProtectedMenuCommand(ToolStripItem item, out string command)
        {
            command = null;
            if (item == null) return false;
            command = ResolveProtectedMenuIdentifier(item.Name);
            if (command != null) return true;

            var tag = item.Tag == null ? string.Empty : item.Tag.ToString();
            command = ResolveProtectedMenuIdentifier(tag);
            if (command != null) return true;

            var title = (item.Text ?? string.Empty)
                .Replace("&", string.Empty)
                .Replace("…", string.Empty)
                .TrimEnd('.', ' ')
                .Trim();
            if (string.Equals(title, "Режимы рисования", StringComparison.CurrentCultureIgnoreCase)) command = "dsettings";
            else if (string.Equals(title, "Библиотека точечных условных знаков", StringComparison.CurrentCultureIgnoreCase)) command = "point_sign_library";
            else if (string.Equals(title, "Библиотека линейных условных знаков", StringComparison.CurrentCultureIgnoreCase)) command = "linear_sign_library";
            else if (string.Equals(title, "Библиотека площадных условных знаков", StringComparison.CurrentCultureIgnoreCase)) command = "area_sign_library";
            else if (string.Equals(title, "Библиотека 3D моделей", StringComparison.CurrentCultureIgnoreCase) ||
                string.Equals(title, "Библиотека 3D-моделей", StringComparison.CurrentCultureIgnoreCase)) command = "models_library";
            else if (string.Equals(title, "Менеджер структуры семантики", StringComparison.CurrentCultureIgnoreCase)) command = "smt_manager";
            else if (string.Equals(title, "Менеджер структуры Smdx", StringComparison.CurrentCultureIgnoreCase)) command = "smdx_manager";
            else if (string.Equals(title, "Настройки материалов", StringComparison.CurrentCultureIgnoreCase)) command = "materials_settings_manager";
            else if (string.Equals(title, "Настройка панелей инструментов", StringComparison.CurrentCultureIgnoreCase)) command = "toolbar_settings";
            return command != null;
        }

        private static string ResolveProtectedMenuIdentifier(string identifier)
        {
            identifier = (identifier ?? string.Empty).Trim();
            var separator = identifier.LastIndexOf('.');
            if (separator >= 0) identifier = identifier.Substring(separator + 1);
            switch (identifier.ToLowerInvariant())
            {
                case "id_drafting_settings": case "dsettings": return "dsettings";
                case "id_point_sign_library": case "point_sign_library": return "point_sign_library";
                case "id_linear_sign_library": case "linear_sign_library": return "linear_sign_library";
                case "id_area_sign_library": case "area_sign_library": return "area_sign_library";
                case "id_models_library": case "models_library": return "models_library";
                case "id_smt_manager": case "smt_manager": return "smt_manager";
                case "id_smdx_manager": case "smdx_manager": return "smdx_manager";
                case "id_materials_settings_manager": case "materials_settings_manager": return "materials_settings_manager";
                case "id_application_settings": case "options": return "options";
                case "id_toolbar_settings": case "toolbar_settings": return "toolbar_settings";
                default: return null;
            }
        }

        private void ActivateEmergencyMode(string reason, bool showNotice)
        {
            if (!_emergencyMode)
            {
                _emergencyMode = true;
                Logger.Info("EMERGENCY MODE activated reason='" + SanitizeLogValue(reason) + "' snapshotCount=" + EmergencyCommandRegistry.Count);
            }

            if (!showNotice || _emergencyNoticeShown) return;
            _emergencyNoticeShown = true;
            ScheduleMessage(
                "Обнаружен сбой реестра команд Robur.\r\n\r\n" +
                "Включён аварийный режим: псевдокоманды, защищённые клавиши и служебные окна Robur запускаются через сохранённые обработчики.\r\n" +
                "Ctrl+Shift+F12 открывает палитру всех сохранённых команд.\r\n\r\n" +
                "Режим действует до перезапуска Robur.");
        }

        private void ScheduleEmergencyPalette()
        {
            CadView cadView;
            if (!TryGetFocusedCadView(out cadView)) return;
            cadView.BeginInvoke(new Action(delegate
            {
                if (_emergencyPalette != null)
                {
                    _emergencyPalette.Activate();
                    return;
                }

                var palette = new EmergencyCommandForm();
                palette.CommandAccepted += delegate(string command) { ScheduleEmergencyCommand(cadView, command, "palette"); };
                palette.FormClosed += delegate { if (ReferenceEquals(_emergencyPalette, palette)) _emergencyPalette = null; };
                _emergencyPalette = palette;
                var owner = cadView.FindForm();
                if (owner == null) palette.Show(); else palette.Show(owner);
            }));
        }

        private void ScheduleEmergencyCommand(CadView cadView, string command, string source)
        {
            if (cadView == null || cadView.IsDisposed || !cadView.IsHandleCreated || _emergencyDispatchPending) return;
            _emergencyDispatchPending = true;
            Logger.Info("emergency dispatch scheduled command='" + command + "' source=" + source);
            try
            {
                cadView.BeginInvoke(new Action(delegate
                {
                    _emergencyDispatchPending = false;
                    string error;
                    var result = EmergencyCommandRegistry.TryExecute(command, new object[0], out error);
                    if (result == EmergencyExecutionResult.Completed ||
                        result == EmergencyExecutionResult.Cancelled)
                        return;

                    var recovery = result == EmergencyExecutionResult.RegistryFailure
                        ? "\r\n\r\nСохранённый обработчик повреждён; требуется перезапуск Robur."
                        : string.Empty;
                    MessageBox.Show(
                        error + recovery + "\r\n\r\nПовторный штатный запуск не выполнялся.",
                        source.EndsWith("safe-snapshot", StringComparison.Ordinal) ? "Безопасный запуск" : "Аварийный запуск",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
            }
            catch
            {
                _emergencyDispatchPending = false;
                throw;
            }
        }
        private void ScheduleMessage(string text)
        {
            CadView cadView;
            try
            {
                cadView = _getCadView();
                if (cadView != null && !cadView.IsDisposed && cadView.IsHandleCreated)
                {
                    cadView.BeginInvoke(new Action(delegate
                    {
                        MessageBox.Show(text, "RoburPseudoCommands", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }));
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("failed to schedule emergency mode notice", ex);
            }
        }

        private static bool TryGetQuickInputBlockReason(CadView cadView, out string reason)
        {
            if (cadView.IsGettingValue)
                reason = "is-getting-value";
            else if (cadView.IsModalEdit)
                reason = "modal-edit";
            else if (CadView.ActionStackCount != 0)
                reason = "action-stack";
            else
            {
                reason = string.Empty;
                return false;
            }

            return true;
        }
        private static bool IsPotentialQuickInputKey(int virtualKey)
        {
            return (virtualKey >= (int)Keys.D0 && virtualKey <= (int)Keys.D9)
                || (virtualKey >= (int)Keys.A && virtualKey <= (int)Keys.Z)
                || (virtualKey >= (int)Keys.NumPad0 && virtualKey <= (int)Keys.NumPad9);
        }

        private void LogQuickInputState(
            string state,
            CadView cadView,
            string firstCharacter,
            bool throttle,
            string reason)
        {
            if (!PluginSettings.IsLogEnabled()) return;
            var now = DateTime.UtcNow;
            if (throttle && now - _lastBlockedQuickInputLogUtc < TimeSpan.FromSeconds(1)) return;
            if (throttle) _lastBlockedQuickInputLogUtc = now;

            try
            {
                var cursor = cadView.CurrentCursor;
                var cursorType = cursor == null ? string.Empty : cursor.GetType().FullName;
                Logger.Info(string.Format(
                    "QuickInput state=''{0}'' firstCharacter=''{1}'' isGettingValue={2} actionStackCount={3} actionTerminated={4} isModalEdit={5} lastUserCmd=''{6}'' currentCursor=''{7}'' emergencyMode={8} reason=''{9}''",
                    state, firstCharacter, cadView.IsGettingValue, CadView.ActionStackCount, CadView.ActionTerminated,
                    cadView.IsModalEdit, SanitizeLogValue(cadView.LastUserCmd), cursorType, _emergencyMode, reason));
            }
            catch (Exception ex) { Logger.Error("failed to log QuickInput state=''" + state + "''", ex); }
        }

        private bool TryGetFocusedCadView(out CadView cadView)
        {
            cadView = _getCadView();
            return CursorAnchor.IsFocusedInside(cadView, GetFocus());
        }

        private void ScheduleQuickInput(CadView cadView, string firstCharacter)
        {
            _popupOpenPending = true;
            try
            {
                cadView.BeginInvoke(new Action(delegate
                {
                    _popupOpenPending = false;
                    try
                    {
                        if (cadView.IsDisposed || !cadView.IsHandleCreated || _popup != null) return;
                        ShowQuickInput(cadView, firstCharacter);
                    }
                    catch (Exception ex) { Logger.Error("failed to open QuickInput", ex); }
                }));
            }
            catch
            {
                _popupOpenPending = false;
                throw;
            }
        }

        private void ShowQuickInput(CadView cadView, string firstCharacter)
        {
            var popup = new QuickInputForm(firstCharacter, Module.IsKnownAlias);
            popup.Location = CursorAnchor.GetPopupLocation(cadView, popup.Size);
            popup.AliasAccepted += delegate(string acceptedAlias)
            {
                ScheduleAliasExecution(cadView, acceptedAlias, "popup", true);
            };
            popup.FormClosed += delegate { if (ReferenceEquals(_popup, popup)) _popup = null; };
            _popup = popup;
            try
            {
                var owner = cadView.FindForm();
                if (owner == null) popup.Show(); else popup.Show(owner);
            }
            catch
            {
                _popup = null;
                popup.Dispose();
                throw;
            }
        }

        private void ScheduleAliasExecution(
            CadView cadView,
            string acceptedAlias,
            string source,
            bool rememberOnSuccess)
        {
            if (string.IsNullOrEmpty(acceptedAlias) || cadView.IsDisposed || !cadView.IsHandleCreated) return;
            var dispatchId = Interlocked.Increment(ref _nextDispatchId);
            var isRepeat = source.StartsWith("repeat-", StringComparison.Ordinal);
            if (isRepeat) _repeatDispatchPending = true;
            Logger.Info((isRepeat ? "repeat" : "QuickInput dispatch") + " scheduled id=" + dispatchId +
                " alias=''" + acceptedAlias + "'' source=" + source);
            try
            {
                cadView.BeginInvoke(new Action(delegate
                {
                    if (isRepeat) _repeatDispatchPending = false;
                    Logger.Info((isRepeat ? "repeat" : "QuickInput dispatch") + " executing id=" + dispatchId +
                        " alias=''" + acceptedAlias + "'' source=" + source);
                    if (Module.ExecuteRegisteredAlias(acceptedAlias, false, dispatchId, source) && rememberOnSuccess)
                    {
                        _lastPopupAlias = acceptedAlias;
                        Logger.Info("repeat stored alias=''" + acceptedAlias + "'' dispatchId=" + dispatchId);
                    }
                }));
            }
            catch (Exception ex)
            {
                if (isRepeat) _repeatDispatchPending = false;
                Logger.Error("failed to schedule alias dispatch id=" + dispatchId + " alias=''" +
                    acceptedAlias + "'' source=" + source, ex);
            }
        }

        private void ClearPopupRepeatHistoryCore(string reason)
        {
            var alias = _lastPopupAlias;
            _lastPopupAlias = null;
            _repeatDispatchPending = false;
            Logger.Info("repeat history cleared alias=''" + (alias ?? string.Empty) + "'' reason=" +
                SanitizeLogValue(reason));
        }

        private void DetachAfterErrors()
        {
            lock (SyncRoot)
            {
                if (!ReferenceEquals(_instance, this)) return;
                Application.RemoveMessageFilter(this);
                _instance = null;
                Logger.Info("keyboard message filter detached after consecutive errors");
            }
        }

        private static bool PostEnter(IntPtr targetHandle)
        {
            var scanCode = MapVirtualKey(VkReturn, MapVkToVsc);
            var keyDownLParam = new IntPtr(1L | ((long)scanCode << 16));
            var keyUpLParam = new IntPtr(1L | ((long)scanCode << 16) | (1L << 30) | (1L << 31));

            if (!PostMessage(targetHandle, WmKeyDown, new IntPtr(VkReturn), keyDownLParam))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to post Enter key-down to Robur.");

            if (!PostMessage(targetHandle, WmKeyUp, new IntPtr(VkReturn), keyUpLParam))
                Logger.Error("failed to post Enter key-up to Robur", new Win32Exception(Marshal.GetLastWin32Error()));

            return true;
        }

        private static bool IsRepeatedKeyDown(IntPtr lParam)
        {
            return (lParam.ToInt64() & (1L << 30)) != 0;
        }

        private static string SanitizeLogValue(string value)
        {
            return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint code, uint mapType);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
    }
}
