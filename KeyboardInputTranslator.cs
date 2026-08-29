using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RoburPseudoCommands
{
    internal static class KeyboardInputTranslator
    {
        private const uint MapVkToVsc = 0;
        private const uint DoNotChangeKeyboardState = 4;

        public static bool TryGetQuickInputCharacter(int virtualKey, IntPtr messageLParam, out string text)
        {
            text = null;

            var keyboardState = new byte[256];
            if (!GetKeyboardState(keyboardState))
                return false;

            var layout = GetKeyboardLayout(0);
            var scanCode = (uint)((messageLParam.ToInt64() >> 16) & 0xff);
            if (scanCode == 0)
                scanCode = MapVirtualKeyEx((uint)virtualKey, MapVkToVsc, layout);

            var buffer = new StringBuilder(8);
            var length = ToUnicodeEx(
                (uint)virtualKey,
                scanCode,
                keyboardState,
                buffer,
                buffer.Capacity,
                DoNotChangeKeyboardState,
                layout);

            if (length <= 0)
                return false;

            var value = buffer.ToString(0, Math.Min(length, buffer.Length));
            if (value.Length != 1 || !char.IsLetterOrDigit(value[0]))
                return false;

            text = value;
            return true;
        }

        [DllImport("user32.dll")]
        private static extern bool GetKeyboardState(byte[] keyboardState);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint threadId);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr keyboardLayout);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ToUnicodeEx(
            uint virtualKey,
            uint scanCode,
            byte[] keyboardState,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder buffer,
            int bufferSize,
            uint flags,
            IntPtr keyboardLayout);
    }
}
