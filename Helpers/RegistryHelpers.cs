using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ZipImageViewer
{
    public static class RegistryHelpers
    {
        #region Supported WIC Decoders
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

            using (var baseKey = Registry.ClassesRoot.OpenSubKey(baseKeyPath)) {
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

        #region Windows Explorer Context Menu
        public static bool CheckExplorerMenuItem(params string[] clsSubDirs) {
            return clsSubDirs.All(clsDir => {
                using (var itmKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{clsDir}\shell\{nameof(ZipImageViewer)}")) {
                    return itmKey != null;
                }
            });
        }

        /// <summary>
        /// Enable menu for certain types of files.
        /// </summary>
        /// <param name="clsSubDirs">The types (keys in Software\Classes) to add menu for.</param>
        public static void SetExplorerMenuItem(params string[] clsSubDirs) {
            // create submenu
            using (var itmKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{nameof(ZipImageViewer)}\shell\OpenWith")) {
                itmKey.SetValue(@"MUIVerb", Helpers.GetRes(@"ttl_OpenWithZIV"), RegistryValueKind.String);
                using (var cmdKey = itmKey.CreateSubKey(@"command")) {
                    cmdKey.SetValue(null, $@"""{App.ExePath}"" ""%1""", RegistryValueKind.String);
                }
            }
            using (var itmKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{nameof(ZipImageViewer)}\shell\PlaySlideshow")) {
                itmKey.SetValue(@"MUIVerb", Helpers.GetRes(@"ttl_PlaySlideshowWithZIV"), RegistryValueKind.String);
                using (var cmdKey = itmKey.CreateSubKey(@"command")) {
                    cmdKey.SetValue(null, $@"""{App.ExePath}"" -slideshow ""%1""", RegistryValueKind.String);
                }
            }

            foreach (var clsDir in clsSubDirs) {
                // create ref to submenu
                using (var zivKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{clsDir}\shell\{nameof(ZipImageViewer)}")) {
                    zivKey.SetValue(@"Icon", $@"""{App.ExePath}""", RegistryValueKind.String);
                    zivKey.SetValue(@"ExtendedSubCommandsKey", nameof(ZipImageViewer), RegistryValueKind.String);
                }
            }
        }

        public static void ClearExplorerMenuItem(params string[] clsSubDirs) {
            // delete ref to submenu
            foreach (var clsDir in clsSubDirs) {
                using (var dirKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{clsDir}", true)) {
                    if (dirKey == null) continue;
                    dirKey.DeleteSubKeyTree($@"shell\{nameof(ZipImageViewer)}", false);
                    //shell key might be created by this program. Delete it when nothing is underneath.
                    using (var shlKey = dirKey.OpenSubKey(@"shell", true)) {
                        if (shlKey != null && shlKey.SubKeyCount == 0 && shlKey.ValueCount == 0) dirKey.DeleteSubKeyTree("shell");
                    }
                }
            }

            // delete submenu
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{nameof(ZipImageViewer)}", false);
        }
        #endregion
    }
}
