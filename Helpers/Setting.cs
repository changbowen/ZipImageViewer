using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using IniParser;
using IniParser.Model;
using SevenZip;
using SizeInt = System.Drawing.Size;
using static ZipImageViewer.TableHelper;
using static ZipImageViewer.SQLiteHelper;
using static ZipImageViewer.SlideshowHelper;
using System.Reflection;
using Newtonsoft.Json;

namespace ZipImageViewer
{
    public class Setting : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public static event EventHandler<PropertyChangedEventArgs> StaticPropertyChanged;

        [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
        sealed class SavedSettingAttribute : Attribute { }


        public static string FilePath => Path.Combine(App.ExeDir, @"config.ini");

        private static void OnStaticPropertyChanged(string propName) {
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propName));
        }

        public enum Transition { None, Random, ZoomFadeBlur, Fade, HorizontalSwipe }
        public enum TransitionSpeed { Fast, Medium, Slow }


        private static string sevenZipDllPath = Path.Combine(App.ExeDir, @"7z.dll");

        private static bool multiThreadUnzip = false;
        [SavedSetting]
        public static bool MultiThreadUnzip {
            get => multiThreadUnzip;
            set {
                if (multiThreadUnzip == value) return;
                multiThreadUnzip = value;
                OnStaticPropertyChanged(nameof(MultiThreadUnzip));
            }
        }

        private static string databaseDir = App.ExeDir;
        [SavedSetting]
        public static string DatabaseDir {
            get => databaseDir;
            set {
                if (databaseDir == value) return;
                databaseDir = value;
                OnStaticPropertyChanged(nameof(DatabaseDir));
            }
        }

        private static ObservablePair<int, int> thumbnailSize = new ObservablePair<int, int>(300, 300);
        [SavedSetting]
        public static ObservablePair<int, int> ThumbnailSize {
            get => thumbnailSize;
            set {
                if (thumbnailSize == value) return;
                thumbnailSize = value;
                OnStaticPropertyChanged(nameof(ThumbnailSize));
            }
        }

        private static Transition viewerTransition = Transition.ZoomFadeBlur;
        [SavedSetting]
        public static Transition ViewerTransition {
            get => viewerTransition;
            set {
                if (viewerTransition == value) return;
                viewerTransition = value;
                OnStaticPropertyChanged(nameof(ViewerTransition));
            }
        }

        private static TransitionSpeed viewerTransitionSpeed = TransitionSpeed.Fast;
        [SavedSetting]
        public static TransitionSpeed ViewerTransitionSpeed {
            get => viewerTransitionSpeed;
            set {
                if (viewerTransitionSpeed == value) return;
                viewerTransitionSpeed = value;
                OnStaticPropertyChanged(nameof(ViewerTransitionSpeed));
            }
        }

        private static double thumbSwapDelayMultiplier = 1d;
        [SavedSetting]
        public static double ThumbSwapDelayMultiplier {
            get => thumbSwapDelayMultiplier;
            set {
                if (thumbSwapDelayMultiplier == value) return;
                thumbSwapDelayMultiplier = value;
                OnStaticPropertyChanged(nameof(ThumbSwapDelayMultiplier));
            }
        }

        private static double thumbDbSize = 2d;
        [SavedSetting]
        public static double ThumbDbSize {
            get => thumbDbSize;
            set {
                if (thumbDbSize == value) return;
                thumbDbSize = value;
                OnStaticPropertyChanged(nameof(ThumbDbSize));
            }
        }

        private static Size lastWindowSize = new Size(1140, 730);
        [SavedSetting]
        public static Size LastWindowSize {
            get => lastWindowSize;
            set {
                if (lastWindowSize == value) return;
                lastWindowSize = value;
                OnStaticPropertyChanged(nameof(LastWindowSize));
            }
        }

        private static string lastPath = "";
        [SavedSetting]
        public static string LastPath {
            get => lastPath;
            set {
                if (lastPath == value) return;
                lastPath = value;
                OnStaticPropertyChanged(nameof(LastPath));
            }
        }

        private static bool liteMode = false;
        [SavedSetting]
        public static bool LiteMode {
            get => liteMode;
            set {
                if (liteMode == value) return;
                liteMode = value;
                OnStaticPropertyChanged(nameof(LiteMode));
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

        private static ObservableKeyedCollection<string, Observable<string>> fallbackPasswords;
        public static ObservableKeyedCollection<string, Observable<string>> FallbackPasswords {
            get => fallbackPasswords;
            set {
                if (fallbackPasswords == value) return;
                fallbackPasswords = value;
                OnStaticPropertyChanged(nameof(FallbackPasswords));
            }
        }

        private static DataTable mappedPasswords;
        public static DataTable MappedPasswords {
            get => mappedPasswords;
            set {
                if (mappedPasswords == value) return;
                mappedPasswords = value;
                OnStaticPropertyChanged(nameof(MappedPasswords));
            }
        }

        private static bool immersionMode;
        //this one is not saved
        public static bool ImmersionMode {
            get => immersionMode;
            set {
                if (immersionMode == value) return;
                immersionMode = value;
                OnStaticPropertyChanged(nameof(ImmersionMode));
            }
        }

        //this one is not used for binding
        [SavedSetting]
        public static SlideAnimConfig SlideAnimConfig { get; set; } = new SlideAnimConfig();

        private static IEnumerable<PropertyInfo> savedSettings => typeof(Setting).GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.GetCustomAttributes(typeof(SavedSettingAttribute), false).Length > 0);

        public static void LoadConfigFromFile(string path = null) {
            if (path == null) path = FilePath;

            //load config
            if (!File.Exists(path)) {
                //initialize default config
                SaveConfigToFile();
            }

            //apply dll path
            SevenZipBase.SetLibraryPath(sevenZipDllPath);

            //parse config file
            var iniData = new FileIniDataParser().ReadFile(path, System.Text.Encoding.UTF8);
            foreach (var prop in savedSettings) {
                var saved = iniData["App Config"][prop.Name];
                if (saved == null) continue;
                prop.SetValue(null, JsonConvert.DeserializeObject(saved, prop.PropertyType));
            }

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
            FallbackPasswords = new ObservableKeyedCollection<string, Observable<string>>(o => o.Item, null,
                iniData["Saved Passwords"].Where(d => d.Value.Length == 0).Select(d => new Observable<string>(d.KeyName)));
            //MappedPasswords = new ObservableKeyedCollection<string, ObservablePair<string, string>>(p => p.Item1, "Item1",
            //iniData["Saved Passwords"].Where(d => d.Value.Length > 0).Select(d => new ObservablePair<string, string>(d.KeyName, d.Value)));

            var mp = Tables[Table.MappedPasswords];
            MappedPasswords = new DataTable(mp.Name);
            MappedPasswords.Columns.Add(nameof(Column.Path), typeof(string));
            MappedPasswords.Columns.Add(nameof(Column.Password), typeof(string));
            MappedPasswords.PrimaryKey = new[] { MappedPasswords.Columns[nameof(Column.Path)] };
            if (File.Exists(mp.FullPath)) {
                try { MappedPasswords.ReadXml(mp.FullPath); }
                catch (Exception ex) {
                    MessageBox.Show("There was an error loading mapped passwords. Loading will be skipped.\r\n" + ex.Message,
                        null, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
        }

        public static void SaveConfigToFile(string path = null) {
            if (path == null) path = FilePath;

            var appConfigs = "[App Config]\r\n";
            foreach (var prop in savedSettings) {
                var value = prop.GetValue(null);
                appConfigs += $"{prop.Name}={JsonConvert.SerializeObject(value)}\r\n";
            }

            var savedPwds = "";
            if (FallbackPasswords != null && FallbackPasswords.Count > 0) {
                foreach (var s in FallbackPasswords) {
                    if (string.IsNullOrWhiteSpace(s.Item)) continue;
                    savedPwds += s.Item + "=\r\n";
                }
            }

            MappedPasswords?.WriteXml(Tables[Table.MappedPasswords].FullPath, XmlWriteMode.WriteSchema);

            File.WriteAllText(path, 
$@"{appConfigs}
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
