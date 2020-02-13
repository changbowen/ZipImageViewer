using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using IniParser;
using IniParser.Model;
using SevenZip;
using SizeInt = System.Drawing.Size;

namespace ZipImageViewer
{
    public class Setting : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public static event EventHandler<PropertyChangedEventArgs> StaticPropertyChanged;

        private static void OnStaticPropertyChanged(string propName) {
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propName));
        }

        public enum Transition { None, Random, ZoomFadeBlur, Fade, HorizontalSwipe }
        public enum TransitionSpeed { Fast, Medium, Slow }


        private static string sevenZipDllPath = @"C:\Program Files\7-Zip\7z.dll";
        public static string SevenZipDllPath {
            get => sevenZipDllPath;
            set {
                if (sevenZipDllPath == value) return;
                sevenZipDllPath = value;
                OnStaticPropertyChanged(nameof(SevenZipDllPath));
            }
        }

        private static string thumbDbDir = App.ExeDir;
        public static string ThumbDbDir {
            get => thumbDbDir;
            set {
                if (thumbDbDir == value) return;
                thumbDbDir = value;
                OnStaticPropertyChanged(nameof(ThumbDbDir));
            }
        }

        private static ObservablePair<int, int> thumbnailSize = new ObservablePair<int, int>(300, 300);
        public static ObservablePair<int, int> ThumbnailSize {
            get => thumbnailSize;
            set {
                if (thumbnailSize == value) return;
                thumbnailSize = value;
                OnStaticPropertyChanged(nameof(ThumbnailSize));
            }
        }

        private static Transition viewerTransition = Transition.ZoomFadeBlur;
        public static Transition ViewerTransition {
            get => viewerTransition;
            set {
                if (viewerTransition == value) return;
                viewerTransition = value;
                OnStaticPropertyChanged(nameof(ViewerTransition));
            }
        }

        private static TransitionSpeed viewerTransitionSpeed = TransitionSpeed.Fast;
        public static TransitionSpeed ViewerTransitionSpeed {
            get => viewerTransitionSpeed;
            set {
                if (viewerTransitionSpeed == value) return;
                viewerTransitionSpeed = value;
                OnStaticPropertyChanged(nameof(ViewerTransitionSpeed));
            }
        }

        private static double thumbDbSize = 2d;
        public static double ThumbDbSize {
            get => thumbDbSize;
            set {
                if (thumbDbSize == value) return;
                thumbDbSize = value;
                OnStaticPropertyChanged(nameof(ThumbDbSize));
            }
        }

        private static Size lastWindowSize = new Size(1280, 800);
        public static Size LastWindowSize {
            get => lastWindowSize;
            set {
                if (lastWindowSize == value) return;
                lastWindowSize = value;
                OnStaticPropertyChanged(nameof(LastWindowSize));
            }
        }

        private static string lastPath = "";
        public static string LastPath {
            get => lastPath;
            set {
                if (lastPath == value) return;
                lastPath = value;
                OnStaticPropertyChanged(nameof(LastPath));
            }
        }

        private static ObservableCollection<ObservableObj> customCommands;
        public static ObservableCollection<ObservableObj> CustomCommands {
            get => customCommands;
            set {
                if (customCommands == value) return;
                customCommands = value;
                OnStaticPropertyChanged(nameof(CustomCommands));
            }
        }

        private static ObservableCollection<Observable<string>> fallbackPasswords;
        public static ObservableCollection<Observable<string>> FallbackPasswords {
            get => fallbackPasswords;
            set {
                if (fallbackPasswords == value) return;
                fallbackPasswords = value;
                OnStaticPropertyChanged(nameof(FallbackPasswords));
            }
        }

        private static ObservableKeyedCollection<string, ObservablePair<string, string>> mappedPasswords;
        public static ObservableKeyedCollection<string, ObservablePair<string, string>> MappedPasswords {
            get => mappedPasswords;
            set {
                if (mappedPasswords == value) return;
                mappedPasswords = value;
                OnStaticPropertyChanged(nameof(MappedPasswords));
            }
        }

        //public static Dictionary<string, string> MappedPasswords;


        public static void LoadConfigFromFile(string path = "config.ini") {
            //load config
            if (!File.Exists(path)) {
                //initialize default config
                SaveConfigToFile();
            }
            
            //parse config file
            var iniData = new FileIniDataParser().ReadFile(path, System.Text.Encoding.UTF8);

            SevenZipDllPath =       ParseConfig(iniData, nameof(SevenZipDllPath),       SevenZipDllPath);
            ThumbDbDir =            ParseConfig(iniData, nameof(ThumbDbDir),            ThumbDbDir);
            ThumbnailSize =         ParseConfig(iniData, nameof(ThumbnailSize),         ThumbnailSize);
            ThumbDbSize =           ParseConfig(iniData, nameof(ThumbDbSize),           ThumbDbSize);
            ViewerTransition =      ParseConfig(iniData, nameof(ViewerTransition),      ViewerTransition);
            ViewerTransitionSpeed = ParseConfig(iniData, nameof(ViewerTransitionSpeed), ViewerTransitionSpeed);
            LastWindowSize =        ParseConfig(iniData, nameof(LastWindowSize),        LastWindowSize);
            LastPath =              ParseConfig(iniData, nameof(LastPath),              LastPath);

            //parse custom commands
            CustomCommands = new ObservableCollection<ObservableObj>();
            var iniCmd = iniData["Custom Commands"];
            if (iniCmd?.Count > 0) {
                foreach (var row in iniCmd) {
                    var cells = row.Value.Split('\t');
                    if (cells.Length < 3) continue;
                    CustomCommands.Add(new ObservableObj(cells[0], cells[1], cells[2]));
                }
            }

            //parse saved passwords at last
            FallbackPasswords = new ObservableCollection<Observable<string>>(
                iniData["Saved Passwords"].Where(d => d.Value.Length == 0).Select(d => new Observable<string>(d.KeyName)));
            MappedPasswords = new ObservableKeyedCollection<string, ObservablePair<string, string>>(p => p.Item1,
                iniData["Saved Passwords"].Where(d => d.Value.Length > 0).Select(d => new ObservablePair<string, string>(d.KeyName, d.Value)));

            //apply dll path
            if (string.IsNullOrEmpty(SevenZipDllPath))
                MessageBox.Show("7z.dll path is missing from the configuration.", "", MessageBoxButton.OK, MessageBoxImage.Error);
            else
                SevenZipBase.SetLibraryPath(SevenZipDllPath);

        }

        private static T ParseConfig<T>(IniData iniData, string key, T defaultVal) {
            var value = iniData["App Config"][key];
            if (value == null) return defaultVal;
            object result = defaultVal;
            switch (defaultVal) {
                case string _:
                    result = value;
                    break;
                case SizeInt _:
                case ObservablePair<int, int> _:
                    int iW, iH;
                    var split1 = value.Split('x', '*', ',');
                    if (split1.Length != 2 || !int.TryParse(split1[0], out iW) || !int.TryParse(split1[1], out iH)) break;
                    if (defaultVal is SizeInt)
                        result = new SizeInt(iW, iH);
                    else
                        result = new ObservablePair<int, int>(iW, iH);
                    break;
                case Size _:
                case ObservablePair<double, double> _:
                    double dW, dH;
                    var split2 = value.Split('x', '*', ',');
                    if (split2.Length != 2 || !double.TryParse(split2[0], out dW) || !double.TryParse(split2[1], out dH)) break;
                    if (defaultVal is Size)
                        result = new Size(dW, dH);
                    else
                        result = new ObservablePair<double, double>(dW, dH);
                    break;
                case int _:
                    int i;
                    if (int.TryParse(value, out i)) result = i;
                    break;
                case double _:
                    double d;
                    if (double.TryParse(value, out d)) result = d;
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
            return (T)result;
        }

        public static void SaveConfigToFile(string path = "config.ini") {
            var savedPwds = "";
            if (FallbackPasswords != null && FallbackPasswords.Count > 0) {
                foreach (var s in FallbackPasswords) {
                    if (string.IsNullOrWhiteSpace(s.Item)) continue;
                    savedPwds += s.Item + "=\r\n";
                }
            }
            if (MappedPasswords != null && MappedPasswords.Count > 0) {
                foreach (var p in MappedPasswords) {
                    if (string.IsNullOrWhiteSpace(p.Item1) || string.IsNullOrWhiteSpace(p.Item2)) continue;
                    savedPwds += p.Item1 + "=" + p.Item2 + "\r\n";
                }
            }


            File.WriteAllText(path, $@"
[App Config]
{nameof(SevenZipDllPath)}={SevenZipDllPath}
{nameof(ThumbDbDir)}={ThumbDbDir}
{nameof(ThumbnailSize)}={ThumbnailSize.Item1}x{ThumbnailSize.Item2}
{nameof(ThumbDbSize)}={ThumbDbSize}
{nameof(ViewerTransition)}={ViewerTransition}
{nameof(ViewerTransitionSpeed)}={ViewerTransitionSpeed}
{nameof(LastWindowSize)}={LastWindowSize.Width}x{LastWindowSize.Height}
{nameof(LastPath)}={LastPath}

[Custom Commands]
{(CustomCommands?.Count > 0 ?
    string.Join("\r\n", CustomCommands.Select(oo => $"{nameof(CustomCommands)}.{CustomCommands.IndexOf(oo)}={oo.Str1}\t{oo.Str2}\t{oo.Str3}")) :
    null)}

;Saved passwords for zipped files. Supported formats:
;password=
;file_full_path=password
[Saved Passwords]
{savedPwds.Trim()}
");
        }
    }
}
