using System.Runtime.InteropServices;
using Chroma.Windowing;

namespace StriveStick
{
    public static class WindowsParts
    {
        public static bool MakeWindowTransparent(Window window)
        {
            window.EnableBorder = false;
            var hwnd = window.SystemWindowHandle;

            SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x00080000);

            return SetLayeredWindowAttributes(hwnd, 0x0000FF00, 0, 1);
        }
        
        [DllImport("User32.dll", CallingConvention = (CallingConvention) 2)]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        
        [DllImport("User32.dll", CallingConvention = (CallingConvention) 2)]
        public static extern long SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);
        
        [DllImport("User32.dll", CallingConvention = (CallingConvention) 2)]
        public static extern long GetWindowLong(IntPtr hWnd, int nIndex);
    }
}