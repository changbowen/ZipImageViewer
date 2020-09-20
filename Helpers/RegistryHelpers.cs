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
        public static bool CheckExplorerMenuItem(string itemKey, params string[] clsSubDirs) {
            return clsSubDirs.All(clsDir => {
                using (var itmKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{clsDir}\shell\{itemKey}")) {
                    return itmKey != null;
                }
            });
        }

        public static void SetExplorerMenuItem(string itemKey, string itemText, string command, params string[] clsSubDirs) {
            foreach (var clsDir in clsSubDirs) {
                using (var itmKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{clsDir}\shell\{itemKey}")) {
                    itmKey.SetValue(null, itemText, RegistryValueKind.String);
                    using (var cmdKey = itmKey.CreateSubKey(@"command")) {
                        cmdKey.SetValue(null, command, RegistryValueKind.String);
                    }
                }
            }
        }

        public static void ClearExplorerMenuItem(string itemKey, params string[] clsSubDirs) {
            foreach (var clsDir in clsSubDirs) {
                using (var dirKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{clsDir}", true)) {
                    if (dirKey == null) continue;
                    dirKey.DeleteSubKeyTree($@"shell\{itemKey}", false);
                    //shell key might be created by this program. Delete it when nothing is underneath.
                    using (var shlKey = dirKey.OpenSubKey(@"shell", true)) {
                        if (shlKey != null && shlKey.SubKeyCount == 0 && shlKey.ValueCount == 0)
                            dirKey.DeleteSubKeyTree("shell");
                    }
                }
            }
        }
        #endregion
    }
}
