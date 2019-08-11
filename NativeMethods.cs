using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;


namespace ZipImageViewer
{
    public static class NativeMethods
    {
        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);
        private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect2 lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hwnd, ref MonitorInfo mInfo);

        [DllImport("Shcore")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiTypes dpiType, out int dpiX, out int dpiY);

        private enum MonitorDpiTypes
        {
            EffectiveDPI = 0,
            AngularDPI = 1,
            RawDPI = 2,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect2
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MonitorInfo
        {
            public uint cbSize;
            public Rect2 rcMonitor;
            public Rect2 rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
        }

        public class DisplayInfo
        {
            public string DeviceName;
            public Rect MonitorArea;
            public Rect WorkArea;
            public int Dpi;
            public bool IsPrimary;
        }

        /// <summary>
        /// When convert is false, return raw values as the application sees them, and does not perform conversion based on DPI.
        /// </summary>
        public static DisplayInfo[] GetDisplayMonitors(bool convert = false)
        {
            var monList = new List<DisplayInfo>();
            //scaling DPI is changed after: changing the primary monitor DPI and then logging off.
            //appears to be the prompt you see when DPI of the primary monitor is changed.
            var scaleDpi = convert ? System.Drawing.Graphics.FromHwnd(IntPtr.Zero).DpiX / 96d : 1d;

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref Rect2 lprcMonitor, IntPtr dwData) {
                    MonitorInfo mi = new MonitorInfo();
                    mi.cbSize = (uint)Marshal.SizeOf(mi);
                    var successInf = GetMonitorInfo(hMonitor, ref mi);
                    if (successInf) {
                        if (GetDpiForMonitor(hMonitor, MonitorDpiTypes.EffectiveDPI, out int dpiX, out int dpiY) != 0) dpiX = 96;
                        //var scaleDpi = dpiX / 96d;
                        var di = new DisplayInfo() {
                            DeviceName = mi.DeviceName,
                            Dpi = dpiX,
                            IsPrimary = mi.dwFlags == 1,
                            MonitorArea = new Rect(new Point(mi.rcMonitor.left / scaleDpi, mi.rcMonitor.top / scaleDpi),
                                                   new Point(mi.rcMonitor.right / scaleDpi, mi.rcMonitor.bottom / scaleDpi)),
                            WorkArea = new Rect(new Point(mi.rcWork.left / scaleDpi, mi.rcWork.top / scaleDpi),
                                               new Point(mi.rcWork.right / scaleDpi, mi.rcWork.bottom / scaleDpi)),
                        };
                        monList.Add(di);
                    }
                    return true;
                }, IntPtr.Zero);
            return monList.ToArray();
        }


        private const int MONITOR_DEFAULTTONULL = 0;
        private const int MONITOR_DEFAULTTOPRIMARY = 1;
        private const int MONITOR_DEFAULTTONEAREST = 2;
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        public static Rect GetMonitorFromWindow(Window win)
        {
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
