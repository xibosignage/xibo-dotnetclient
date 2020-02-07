using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XiboClient.Logic
{
    public delegate void MouseInterceptorEventHandler();

    /// <summary>
    /// Adapted From: http://blogs.msdn.com/b/toub/archive/2006/05/03/589468.aspx
    /// </summary>
    class MouseInterceptor
    {
        private static LowLevelMouseProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        private static MouseInterceptor s_instance = null;
        // The KeyPressed Event
        public event MouseInterceptorEventHandler MouseEvent;

        private static System.Drawing.Point _mouseLocation;

        public static IntPtr SetHook()
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        public static void UnsetHook()
        {
            UnhookWindowsHookEx(_hookID);
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                if (MouseMessages.WM_LBUTTONDOWN == (MouseMessages)wParam || MouseMessages.WM_MOUSEMOVE == (MouseMessages)wParam)
                {
                    MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    Console.WriteLine(hookStruct.pt.x + ", " + hookStruct.pt.y);

                    if (Math.Abs(_mouseLocation.X - hookStruct.pt.x) > 5 || Math.Abs(_mouseLocation.Y - hookStruct.pt.y) > 5)
                    {
                        if (MouseInterceptor.s_instance != null && MouseInterceptor.s_instance.MouseEvent != null)
                            MouseInterceptor.s_instance.MouseEvent();
                    }

                    _mouseLocation = new System.Drawing.Point(hookStruct.pt.x, hookStruct.pt.y);
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
                    s_instance = new MouseInterceptor();

                return s_instance;
            }
        }

        private const int WH_MOUSE_LL = 14;

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
