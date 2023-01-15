using Microsoft.WindowsAPICodePack.ApplicationServices;
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

        #region Natual Sort
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern int StrCmpLogicalW(string psz1, string psz2);

        public class NaturalStringComparer : IComparer<string>
        {
            private readonly int modifier = 1;

            public NaturalStringComparer() : this(false) { }
            public NaturalStringComparer(bool descending) {
                if (descending) modifier = -1;
            }

            public int Compare(string a, string b) {
                return StrCmpLogicalW(a ?? "", b ?? "") * modifier;
            }

            public static int Compare(string a, string b, bool descending = false) {
                return StrCmpLogicalW(a ?? "", b ?? "") * (descending ? -1 : 1);
            }
        }
        #endregion

        #region Get File Icon
        public static System.Windows.Media.ImageSource GetIcon(string path, bool smallIcon, bool isDirectory) {
            // SHGFI_USEFILEATTRIBUTES takes the file name and attributes into account if it doesn't exist
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;
            if (smallIcon) flags |= SHGFI_SMALLICON;

            uint attributes = FILE_ATTRIBUTE_NORMAL;
            if (isDirectory) attributes |= FILE_ATTRIBUTE_DIRECTORY;

            if (0 != SHGetFileInfo(path, attributes, out SHFILEINFO shfi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), flags)) {
                var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(shfi.hIcon, Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                DestroyIcon(shfi.hIcon);
                return source;
            }
            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32")]
        private static extern int SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
        private const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
        private const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        private const uint FILE_ATTRIBUTE_ARCHIVE = 0x00000020;
        private const uint FILE_ATTRIBUTE_DEVICE = 0x00000040;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const uint FILE_ATTRIBUTE_TEMPORARY = 0x00000100;
        private const uint FILE_ATTRIBUTE_SPARSE_FILE = 0x00000200;
        private const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
        private const uint FILE_ATTRIBUTE_COMPRESSED = 0x00000800;
        private const uint FILE_ATTRIBUTE_OFFLINE = 0x00001000;
        private const uint FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x00002000;
        private const uint FILE_ATTRIBUTE_ENCRYPTED = 0x00004000;
        private const uint FILE_ATTRIBUTE_VIRTUAL = 0x00010000;

        private const uint SHGFI_ICON = 0x000000100;     // get icon
        private const uint SHGFI_DISPLAYNAME = 0x000000200;     // get display name
        private const uint SHGFI_TYPENAME = 0x000000400;     // get type name
        private const uint SHGFI_ATTRIBUTES = 0x000000800;     // get attributes
        private const uint SHGFI_ICONLOCATION = 0x000001000;     // get icon location
        private const uint SHGFI_EXETYPE = 0x000002000;     // return exe type
        private const uint SHGFI_SYSICONINDEX = 0x000004000;     // get system icon index
        private const uint SHGFI_LINKOVERLAY = 0x000008000;     // put a link overlay on icon
        private const uint SHGFI_SELECTED = 0x000010000;     // show icon in selected state
        private const uint SHGFI_ATTR_SPECIFIED = 0x000020000;     // get only specified attributes
        private const uint SHGFI_LARGEICON = 0x000000000;     // get large icon
        private const uint SHGFI_SMALLICON = 0x000000001;     // get small icon
        private const uint SHGFI_OPENICON = 0x000000002;     // get open icon
        private const uint SHGFI_SHELLICONSIZE = 0x000000004;     // get shell size icon
        private const uint SHGFI_PIDL = 0x000000008;     // pszPath is a pidl
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;     // use passed dwFileAttribute
        #endregion

        #region Prevent Sleep
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [Flags]
        private enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
        }

        /// <summary>
        /// Prevent turning screen off and / or sleeping.
        /// </summary>
        /// <param name="level">Set to 2 to configure both ES_DISPLAY_REQUIRED and ES_SYSTEM_REQUIRED flags.
        /// Set to 1 to configure ES_SYSTEM_REQUIRED flag only.
        /// Set to 0 to clear previous configs.</param>
        public static void SetPowerState(int level = 0)
        {
            if (level > 1)
                SetThreadExecutionState(EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
            else if (level == 1)
                SetThreadExecutionState(EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
            else
                SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
        }
        #endregion
    }
}
