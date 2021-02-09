using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace XiboClient.Logic
{
    /// <summary>
    /// Adapted From: http://blogs.msdn.com/b/toub/archive/2006/05/03/589468.aspx
    /// </summary>
    class MouseInterceptor
    {
        private static LowLevelMouseProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        private static MouseInterceptor s_instance = null;

        // Events
        public delegate void MouseMoveDelegate();
        public event MouseMoveDelegate MouseMoveEvent;

        public delegate void MouseClickDelegate(Point point);
        public event MouseClickDelegate MouseClickEvent;

        /// <summary>
        /// The mouse location
        /// </summary>
        private static Point _mouseLocation;

        /// <summary>
        /// Set the hook
        /// </summary>
        /// <returns></returns>
        public static IntPtr SetHook()
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        /// <summary>
        /// Unset the hook
        /// </summary>
        public static void UnsetHook()
        {
            UnhookWindowsHookEx(_hookID);
        }

        /// <summary>
        /// Low level mouse proc
        /// </summary>
        /// <param name="nCode"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Hook Callback
        /// </summary>
        /// <param name="nCode"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // Process various types of message
                if (MouseMessages.WM_MOUSEMOVE == (MouseMessages)wParam)
                {
                    // Mouse has moved
                    MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                    // Has it moved more than 5 pixels in any direction
                    bool moved = Math.Abs(_mouseLocation.X - hookStruct.pt.x) > 5 || Math.Abs(_mouseLocation.Y - hookStruct.pt.y) > 5;

                    // Set new point
                    _mouseLocation = new Point(hookStruct.pt.x, hookStruct.pt.y);

                    // Moved?
                    if (moved && s_instance != null)
                    {
                        s_instance.MouseMoveEvent?.Invoke();
                    }
                } 
                else if (MouseMessages.WM_LBUTTONDOWN == (MouseMessages)wParam)
                {
                    // Mouse down
                    if (s_instance != null)
                    {
                        s_instance.MouseClickEvent?.Invoke(_mouseLocation);
                    }
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        /// <summary>
        /// Returns the singleton instance.
        /// </summary>
        public static MouseInterceptor Instance
        {
            get
            {
                if (null == s_instance)
                {
                    s_instance = new MouseInterceptor();
                }

                return s_instance;
            }
        }

        /// <summary>
        /// LL (low level) to catch sub proceses
        /// </summary>
        private const int WH_MOUSE_LL = 14;

        /// <summary>
        /// Move Message
        /// </summary>
        private enum MouseMessages
        {
            WM_LBUTTONDOWN = 0x0201,
            WM_LBUTTONUP = 0x0202,
            WM_MOUSEMOVE = 0x0200,
            WM_MOUSEWHEEL = 0x020A,
            WM_RBUTTONDOWN = 0x0204,
            WM_RBUTTONUP = 0x0205
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

    }
}
