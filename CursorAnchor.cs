using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Topomatic.Cad.View;

namespace RoburPseudoCommands
{
    internal static class CursorAnchor
    {
        public static bool IsFocusedInside(CadView cadView, IntPtr focusedHandle)
        {
            if (cadView == null || cadView.IsDisposed || !cadView.IsHandleCreated || focusedHandle == IntPtr.Zero)
                return false;

            return focusedHandle == cadView.Handle || IsChild(cadView.Handle, focusedHandle);
        }

        public static Point GetPopupLocation(CadView cadView, Size popupSize)
        {
            var point = Cursor.Position;
            point.Offset(12, 18);

            var screen = cadView == null
                ? Screen.FromPoint(point)
                : Screen.FromControl(cadView);
            var bounds = screen.WorkingArea;

            point.X = Math.Max(bounds.Left, Math.Min(point.X, bounds.Right - popupSize.Width));
            point.Y = Math.Max(bounds.Top, Math.Min(point.Y, bounds.Bottom - popupSize.Height));
            return point;
        }

        [DllImport("user32.dll")]
        private static extern bool IsChild(IntPtr parentHandle, IntPtr childHandle);
    }
}
