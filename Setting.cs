using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using IniParser;
using IniParser.Model;
using SevenZip;

namespace ZipImageViewer
{
    public static class Setting
    {
        public static IniData iniData;
        public static string SevenZipDllPath = @"C:\Program Files\7-Zip\7z.dll";
        public static System.Drawing.Size ThumbnailSize = new System.Drawing.Size(300, 200);
        public static string[] FallbackPasswords;
        public static Dictionary<string, string> MappedPasswords;


        public static void LoadConfigFromFile(string path = "config.ini") {
            //load config
            if (!File.Exists(path)) {
                //initialize default config
                SaveConfigToFile();
            }
            
            //parse config file
            iniData = new FileIniDataParser().ReadFile(path, System.Text.Encoding.UTF8);
            //parse dll path
            SevenZipDllPath = iniData["App Config"]["SevenZipDllPath"];
            //parse thumb size
            var thumbsize = iniData["App Config"]["ThumbnailSize"]?.Split('x', '*', ',');
            if (thumbsize?.Length == 2)
                ThumbnailSize = new System.Drawing.Size(int.Parse(thumbsize[0]), int.Parse(thumbsize[1]));
            //parse saved passwords
            FallbackPasswords = iniData["Saved Passwords"].Where(d => d.Value.Length == 0).Select(d => d.KeyName).ToArray();
            MappedPasswords = iniData["Saved Passwords"].Where(d => d.Value.Length > 0).ToDictionary(d => d.KeyName, d => d.Value);


            //apply dll path
            if (string.IsNullOrEmpty(SevenZipDllPath))
                MessageBox.Show("7z.dll path is missing from the configuration.", "", MessageBoxButton.OK, MessageBoxImage.Error);
            else
                SevenZipBase.SetLibraryPath(SevenZipDllPath);

        }

        public static void SaveConfigToFile(string path = "config.ini", string serPwds = null) {
            File.WriteAllText(path, $@"
[App Config]
SevenZipDllPath={SevenZipDllPath}
ThumbnailSize={ThumbnailSize.Width}x{ThumbnailSize.Height}

;Saved passwords for zipped files. Supported formats:
;password=
;file_full_path=password
[Saved Passwords]
{serPwds ?? SerializePasswords().Trim()}
");
        }

        public static string SerializePasswords() {
            var savedPwds = "";
            if (FallbackPasswords != null && FallbackPasswords.Length > 0) {
                foreach (var s in FallbackPasswords) {
                    savedPwds += s + "=\r\n";
                }
            }
            if (MappedPasswords != null && MappedPasswords.Count > 0) {
                foreach (var kvp in MappedPasswords) {
                    savedPwds += kvp.Key + "=" + kvp.Value + "\r\n";
                }
            }
            return savedPwds;
        }
    }
}
