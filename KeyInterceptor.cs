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
        private bool _popupOpenPending;
        private bool _repeatDispatchPending;
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

        public static void Attach(Func<CadView> getCadView)
        {
            if (getCadView == null) throw new ArgumentNullException("getCadView");
            lock (SyncRoot)
            {
                if (_instance != null) return;
                var instance = new KeyInterceptor(getCadView);
                Application.AddMessageFilter(instance);
                _instance = instance;
                Logger.Info("input message filter attached; Space acts as Enter");
            }
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

            if (_popup != null) return false;
            if (_popupOpenPending) return true;

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
                        SanitizeLogValue(cadView.LastUserCmd) + "'");
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
                    "QuickInput state=''{0}'' firstCharacter=''{1}'' isGettingValue={2} actionStackCount={3} actionTerminated={4} isModalEdit={5} lastUserCmd=''{6}'' currentCursor=''{7}'' reason=''{8}''",
                    state, firstCharacter, cadView.IsGettingValue, CadView.ActionStackCount, CadView.ActionTerminated,
                    cadView.IsModalEdit, SanitizeLogValue(cadView.LastUserCmd), cursorType, reason));
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
