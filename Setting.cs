using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using IniParser;
using IniParser.Model;
using SevenZip;
using SizeInt = System.Drawing.Size;

namespace ZipImageViewer
{
    public static class Setting
    {
        public enum Transition { None, Random, ZoomFadeBlur, Fade, HorizontalSwipe }
        public enum TransitionSpeed { Fast, Medium, Slow }

        public static string SevenZipDllPath = @"C:\Program Files\7-Zip\7z.dll";
        public static SizeInt ThumbnailSize = new SizeInt(300, 200);
        public static Transition ViewerTransition = Transition.ZoomFadeBlur;
        public static TransitionSpeed ViewerTransitionSpeed = TransitionSpeed.Fast;
        public static int ThumbDbSize = 2048;
        public static Size LastWindowSize = new Size(1200, 640);
        public static string LastPath = "";

        public static string[] FallbackPasswords;
        public static Dictionary<string, string> MappedPasswords;


        public static void LoadConfigFromFile(string path = "config.ini") {
            //load config
            if (!File.Exists(path)) {
                //initialize default config
                SaveConfigToFile();
            }
            
            //parse config file
            var iniData = new FileIniDataParser().ReadFile(path, System.Text.Encoding.UTF8);

            ParseConfig(iniData, nameof(SevenZipDllPath),       ref SevenZipDllPath);
            ParseConfig(iniData, nameof(ThumbnailSize),         ref ThumbnailSize);
            ParseConfig(iniData, nameof(ThumbDbSize),           ref ThumbDbSize);
            ParseConfig(iniData, nameof(ViewerTransition),      ref ViewerTransition);
            ParseConfig(iniData, nameof(ViewerTransitionSpeed), ref ViewerTransitionSpeed);
            ParseConfig(iniData, nameof(LastWindowSize),        ref LastWindowSize);
            ParseConfig(iniData, nameof(LastPath),              ref LastPath);

            //parse saved passwords at last
            FallbackPasswords = iniData["Saved Passwords"].Where(d => d.Value.Length == 0).Select(d => d.KeyName).ToArray();
            MappedPasswords = iniData["Saved Passwords"].Where(d => d.Value.Length > 0).ToDictionary(d => d.KeyName, d => d.Value);

            //apply dll path
            if (string.IsNullOrEmpty(SevenZipDllPath))
                MessageBox.Show("7z.dll path is missing from the configuration.", "", MessageBoxButton.OK, MessageBoxImage.Error);
            else
                SevenZipBase.SetLibraryPath(SevenZipDllPath);

        }

        private static void ParseConfig<T>(IniData iniData, string key, ref T output) {
            var value = iniData["App Config"][key];
            if (value == null) return;
            object result = null;
            switch (output) {
                case string _:
                    result = value;
                    break;
                case SizeInt _:
                    int iW, iH;
                    var split1 = value.Split('x', '*', ',');
                    if (split1.Length == 2 && int.TryParse(split1[0], out iW) && int.TryParse(split1[1], out iH))
                        result = new SizeInt(iW, iH);
                    break;
                case Size _:
                    double dW, dH;
                    var split2 = value.Split('x', '*', ',');
                    if (split2.Length == 2 && double.TryParse(split2[0], out dW) && double.TryParse(split2[1], out dH))
                        result = new Size(dW, dH);
                    break;
                case int _:
                    int i;
                    if (int.TryParse(value, out i)) result = i;
                    break;
                case Transition _:
                    Transition t;
                    if (Enum.TryParse(value, out t)) result = t;
                    break;
                case TransitionSpeed _:
                    TransitionSpeed ts;
                    if (Enum.TryParse(value, out ts)) result = ts;
                    break;
            }
            output = (T)result;
        }

        public static void SaveConfigToFile(string path = "config.ini", string serPwds = null) {
            File.WriteAllText(path, $@"
[App Config]
{nameof(SevenZipDllPath)}={SevenZipDllPath}
{nameof(ThumbnailSize)}={ThumbnailSize.Width}x{ThumbnailSize.Height}
{nameof(ThumbDbSize)}={ThumbDbSize}
{nameof(ViewerTransition)}={ViewerTransition}
{nameof(ViewerTransitionSpeed)}={ViewerTransitionSpeed}
{nameof(LastWindowSize)}={LastWindowSize.Width}x{LastWindowSize.Height}
{nameof(LastPath)}={LastPath}

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
