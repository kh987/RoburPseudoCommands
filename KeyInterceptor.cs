using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Topomatic.Cad.View;

namespace RoburPseudoCommands
{
    internal sealed class KeyInterceptor : IMessageFilter
    {
        private const int WmKeyDown = 0x0100;
        private const int WmKeyUp = 0x0101;
        private const int VkReturn = 0x0d;
        private const int VkSpace = 0x20;
        private const uint MapVkToVsc = 0;

        private static readonly object SyncRoot = new object();
        private static KeyInterceptor _instance;

        private readonly Func<CadView> _getCadView;
        private QuickInputForm _popup;
        private int _consecutiveErrors;

        private KeyInterceptor(Func<CadView> getCadView)
        {
            _getCadView = getCadView;
        }

        public static bool IsAttached
        {
            get
            {
                lock (SyncRoot)
                {
                    return _instance != null;
                }
            }
        }

        public static void Attach(Func<CadView> getCadView)
        {
            if (getCadView == null)
                throw new ArgumentNullException("getCadView");

            lock (SyncRoot)
            {
                if (_instance != null)
                    return;

                var instance = new KeyInterceptor(getCadView);
                Application.AddMessageFilter(instance);
                _instance = instance;
                Logger.Info("keyboard message filter attached");
            }
        }

        public bool PreFilterMessage(ref Message message)
        {
            if (message.Msg != WmKeyDown)
                return false;

            try
            {
                var handled = HandleKeyDown(message);
                _consecutiveErrors = 0;
                return handled;
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                Logger.Error("keyboard message filter failed consecutiveErrors=" + _consecutiveErrors, ex);
                if (_consecutiveErrors >= 3)
                    DetachAfterErrors();

                return false;
            }
        }

        private bool HandleKeyDown(Message message)
        {
            if (_popup != null)
                return false;

            if (IsRepeatedKeyDown(message.LParam))
                return false;

            var quickInputEnabled = PluginSettings.IsQuickInputEnabled();
            var spaceActsAsEnter = PluginSettings.IsSpaceActsAsEnterEnabled();
            if (!quickInputEnabled && !spaceActsAsEnter)
                return false;

            if ((Control.ModifierKeys & (Keys.Control | Keys.Alt | Keys.Shift)) != Keys.None)
                return false;

            CadView cadView;
            IntPtr focusedHandle;
            if (!TryGetFocusedCadView(out cadView, out focusedHandle))
                return false;

            var virtualKey = message.WParam.ToInt32();
            if (quickInputEnabled && !cadView.IsGettingValue)
            {
                string firstCharacter;
                if (KeyboardInputTranslator.TryGetQuickInputCharacter(virtualKey, message.LParam, out firstCharacter))
                {
                    ShowQuickInput(cadView, firstCharacter);
                    return true;
                }
            }

            if (spaceActsAsEnter && virtualKey == VkSpace)
                return PostEnter(focusedHandle);

            return false;
        }

        private bool TryGetFocusedCadView(out CadView cadView, out IntPtr focusedHandle)
        {
            cadView = _getCadView();
            focusedHandle = GetFocus();

            return CursorAnchor.IsFocusedInside(cadView, focusedHandle);
        }

        private void ShowQuickInput(CadView cadView, string firstCharacter)
        {
            var popup = new QuickInputForm(firstCharacter, Module.IsKnownAlias);
            popup.Location = CursorAnchor.GetPopupLocation(cadView, popup.Size);
            popup.FormClosed += delegate
            {
                var acceptedAlias = popup.AcceptedAlias;
                _popup = null;
                popup.Dispose();

                if (string.IsNullOrEmpty(acceptedAlias) || cadView.IsDisposed || !cadView.IsHandleCreated)
                    return;

                try
                {
                    cadView.BeginInvoke(new Action(delegate
                    {
                        Module.ExecuteRegisteredAlias(acceptedAlias, false);
                    }));
                }
                catch (Exception ex)
                {
                    Logger.Error("failed to schedule QuickInput alias='" + acceptedAlias + "'", ex);
                }
            };

            _popup = popup;
            try
            {
                var owner = cadView.FindForm();
                if (owner == null)
                    popup.Show();
                else
                    popup.Show(owner);
            }
            catch
            {
                _popup = null;
                popup.Dispose();
                throw;
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
            {
                Logger.Error(
                    "failed to post Enter key-up to Robur",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            return true;
        }

        private void DetachAfterErrors()
        {
            lock (SyncRoot)
            {
                if (!ReferenceEquals(_instance, this))
                    return;

                Application.RemoveMessageFilter(this);
                _instance = null;
                Logger.Info("keyboard message filter detached after consecutive errors");
            }
        }

        private static bool IsRepeatedKeyDown(IntPtr lParam)
        {
            return (lParam.ToInt64() & (1L << 30)) != 0;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint code, uint mapType);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
    }
}
