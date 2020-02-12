using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ZipImageViewer
{
    public static class NativeHelpers
    {
        ////hide taskbar
        //[DllImport("user32.dll")]
        //private static extern int FindWindow(string className, string windowText);
        //[DllImport("user32.dll")]
        //private static extern int ShowWindow(int hwnd, int command);
        //private const int SW_HIDE = 0;
        //private const int SW_SHOW = 1;

        //public static void HideTaskbar() {
        //    ShowWindow(FindWindow("Shell_TrayWnd", ""), SW_HIDE);
        //}
        //public static void ShowTaskbar() {
        //    ShowWindow(FindWindow("Shell_TrayWnd", ""), SW_SHOW);
        //}


        //get monitor
        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public uint cbSize;
            public Rect2 rcMonitor;
            public Rect2 rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect2
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        private const int MONITOR_DEFAULTTONULL = 0;
        private const int MONITOR_DEFAULTTOPRIMARY = 1;
        private const int MONITOR_DEFAULTTONEAREST = 2;
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);
        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hwnd, ref MonitorInfo mInfo);

        public static Rect GetMonitorFromWindow(Window win) {
            var mi = new MonitorInfo();
            mi.cbSize = (uint)Marshal.SizeOf(mi);
            var hwmon = MonitorFromWindow(new System.Windows.Interop.WindowInteropHelper(win).EnsureHandle(), MONITOR_DEFAULTTONULL);
            if (hwmon != null && GetMonitorInfo(hwmon, ref mi)) {
                //convert to device-independent vaues
                var mon = mi.rcMonitor;
                Point realp1;
                Point realp2;
                var trans = PresentationSource.FromVisual(win).CompositionTarget.TransformFromDevice;
                realp1 = trans.Transform(new Point(mon.left, mon.top));
                realp2 = trans.Transform(new Point(mon.right, mon.bottom));
                return new Rect(realp1, realp2);
            }
            else
                throw new Exception("Failed to get monitor info.");
        }
    }
}
