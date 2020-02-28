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
        #region Monitor Info
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
        #endregion

        #region Supported WIC decoders
        /// <summary>
        /// GUID of the component registration group for WIC decoders
        /// </summary>
        private const string WICDecoderCategory = @"{7ED96837-96F0-4812-B211-F13C24117ED3}";
        
        public static List<string> GetWICDecoders() {
            var result = new List<string>(new string[] { ".BMP", ".GIF", ".ICO", ".JPEG", ".PNG", ".TIFF", ".DDS", ".JPG", ".JXR", ".HDP", ".WDP" });
            string baseKeyPath;

            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
                baseKeyPath = "Wow6432Node\\CLSID";
            else
                baseKeyPath = "CLSID";

            using (var baseKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(baseKeyPath)) {
                if (baseKey == null) return null;
                using (var categoryKey = baseKey.OpenSubKey(WICDecoderCategory + @"\instance", false)) {
                    if (categoryKey == null) return null;
                    // Read the guids of the registered decoders
                    var codecGuids = categoryKey.GetSubKeyNames();

                    foreach (var codecGuid in codecGuids) {
                        // Read the properties of the single registered decoder
                        using (var codecKey = baseKey.OpenSubKey(codecGuid)) {
                            if (codecKey == null) continue;
                            var split = codecKey.GetValue("FileExtensions", "").ToString().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            result.AddRange(split);
                        }
                    }
                    return result;
                }

            }
        }
        #endregion


    }
}
