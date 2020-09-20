using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using IniParser;
using SevenZip;
using SizeInt = System.Drawing.Size;
using static ZipImageViewer.Helpers;
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
        private static void OnStaticPropertyChanged(string propName) {
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propName));
        }

        [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
        sealed class AppConfigAttribute : Attribute { }

        public enum Transition
        {
            [Description("ttl_" + nameof(None), true)]
            None,
            [Description("ttl_" + nameof(Random), true)]
            Random,
            [Description("ttl_" + nameof(ZoomFadeBlur), true)]
            ZoomFadeBlur,
            [Description("ttl_" + nameof(Fade), true)]
            Fade,
            [Description("ttl_" + nameof(HorizontalSwipe), true)]
            HorizontalSwipe
        }
        public enum TransitionSpeed
        {
            [Description("ttl_" + nameof(Fast), true)]
            Fast,
            [Description("ttl_" + nameof(Medium), true)]
            Medium,
            [Description("ttl_" + nameof(Slow), true)]
            Slow
        }
        public enum Background
        {
            [Description("ttl_" + nameof(DarkCheckerboard), true)]
            DarkCheckerboard,
            [Description("ttl_" + nameof(DarkLinear), true)]
            DarkLinear,
            [Description("ttl_" + nameof(Black), true)]
            Black,
            [Description("ttl_" + nameof(Grey), true)]
            Grey,
            [Description("ttl_" + nameof(White), true)]
            White
        }

        private enum ConfigSection
        { AppConfig, CustomCommands, FallbackPasswords }

        public static string FilePath => Path.Combine(App.ExeDir, @"config.ini");

        private static string sevenZipDllPath => Path.Combine(App.ExeDir, @"7z.dll");


        private static string databaseDir = App.ExeDir;
        [AppConfig]
        public static string DatabaseDir {
            get => databaseDir;
            set {
                if (databaseDir == value) return;
                databaseDir = value;
                OnStaticPropertyChanged(nameof(DatabaseDir));
            }
        }

        private static ObservablePair<int, int> thumbnailSize = new ObservablePair<int, int>(300, 300);
        [AppConfig]
        public static ObservablePair<int, int> ThumbnailSize {
            get => thumbnailSize;
            set {
                if (thumbnailSize == value) return;
                thumbnailSize = value;
                OnStaticPropertyChanged(nameof(ThumbnailSize));
            }
        }

        private static Transition viewerTransition = Transition.ZoomFadeBlur;
        [AppConfig]
        public static Transition ViewerTransition {
            get => viewerTransition;
            set {
                if (viewerTransition == value) return;
                viewerTransition = value;
                OnStaticPropertyChanged(nameof(ViewerTransition));
            }
        }

        private static TransitionSpeed viewerTransitionSpeed = TransitionSpeed.Fast;
        [AppConfig]
        public static TransitionSpeed ViewerTransitionSpeed {
            get => viewerTransitionSpeed;
            set {
                if (viewerTransitionSpeed == value) return;
                viewerTransitionSpeed = value;
                OnStaticPropertyChanged(nameof(ViewerTransitionSpeed));
            }
        }

        private static Background viewerBackground = Background.DarkCheckerboard;
        [AppConfig]
        public static Background ViewerBackground {
            get => viewerBackground;
            set {
                if (viewerBackground == value) return;
                viewerBackground = value;
                OnStaticPropertyChanged(nameof(ViewerBackground));
            }
        }

        private static double thumbSwapDelayMultiplier = 1d;
        [AppConfig]
        public static double ThumbSwapDelayMultiplier {
            get => thumbSwapDelayMultiplier;
            set {
                if (thumbSwapDelayMultiplier == value) return;
                thumbSwapDelayMultiplier = value;
                OnStaticPropertyChanged(nameof(ThumbSwapDelayMultiplier));
            }
        }

        private static double thumbDbSize = 2d;
        [AppConfig]
        public static double ThumbDbSize {
            get => thumbDbSize;
            set {
                if (thumbDbSize == value) return;
                thumbDbSize = value;
                OnStaticPropertyChanged(nameof(ThumbDbSize));
            }
        }

        private static Size lastWindowSize = new Size(1140, 730);
        [AppConfig]
        public static Size LastWindowSize {
            get => lastWindowSize;
            set {
                if (lastWindowSize == value) return;
                lastWindowSize = value;
                OnStaticPropertyChanged(nameof(LastWindowSize));
            }
        }

        private static string lastPath = "";
        [AppConfig]
        public static string LastPath {
            get => lastPath;
            set {
                if (lastPath == value) return;
                lastPath = value;
                OnStaticPropertyChanged(nameof(LastPath));
            }
        }

        private static bool liteMode = false;
        [AppConfig]
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

        private static SlideAnimConfig slideAnimConfig = new SlideAnimConfig();
        [AppConfig]
        public static SlideAnimConfig SlideAnimConfig {
            get => slideAnimConfig;
            set {
                if (slideAnimConfig == value) return;
                slideAnimConfig = value;
                OnStaticPropertyChanged(nameof(SlideAnimConfig));
            }
        }


        #region non-saved settings

        private static bool immersionMode;
        public static bool ImmersionMode {
            get => immersionMode;
            set {
                if (immersionMode == value) return;
                immersionMode = value;
                OnStaticPropertyChanged(nameof(ImmersionMode));
            }
        }

        public bool ExpMenuSlideshow {
            get => RegistryHelpers.CheckExplorerMenuItem(@"ZIV_PlaySlideshow", @"*", @"Directory");
            set {
                if (value) {
                    RegistryHelpers.SetExplorerMenuItem(@"ZIV_PlaySlideshow", GetRes(@"ttl_PlaySlideshowWithZIV"),
                        $@"""{Assembly.GetExecutingAssembly().Location}"" -slideshow ""%1""", @"*", @"Directory");
                }
                else {
                    RegistryHelpers.ClearExplorerMenuItem(@"ZIV_PlaySlideshow", @"*", @"Directory");
                }
                OnStaticPropertyChanged(nameof(ExpMenuSlideshow));
            }
        }

        public static Rect LastViewWindowRect;

        #endregion


        private static IEnumerable<PropertyInfo> appConfigs => typeof(Setting).GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.GetCustomAttributes(typeof(AppConfigAttribute), false).Length > 0);

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
            foreach (var prop in appConfigs) {
                var saved = iniData[nameof(ConfigSection.AppConfig)][prop.Name];
                if (saved == null) continue;
                try { prop.SetValue(null, JsonConvert.DeserializeObject(saved, prop.PropertyType)); }
                catch {
                    MessageBox.Show(GetRes(@"msg_ErrorLoadConfig", prop.Name), string.Empty, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }

            //parse custom commands
            CustomCommands = new ObservableCollection<ObservableObj>();
            try {
                var iniCmd = iniData[nameof(ConfigSection.CustomCommands)];
                if (iniCmd?.Count > 0) {
                    foreach (var row in iniCmd) {
                        var ary = new string[3];
                        row.Value.Split('\t').CopyTo(ary, 0);
                        CustomCommands.Add(new ObservableObj(ary[0], ary[1], ary[2]));
                    }
                }
            }
            catch {
                MessageBox.Show(GetRes(@"msg_ErrorLoadConfig", GetRes(@"ttl_CustomCommands")), string.Empty, MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }

            //parse lists at last
            FallbackPasswords = new ObservableKeyedCollection<string, Observable<string>>(o => o.Item);
            try {
                FallbackPasswords.AddRange(iniData[nameof(ConfigSection.FallbackPasswords)]
                    .Where(d => d.Value.Length == 0)
                    .Select(d => new Observable<string>(d.KeyName)));
            }
            catch {
                MessageBox.Show(GetRes(@"msg_ErrorLoadConfig", GetRes(@"ttl_FallbackPasswords")), string.Empty, MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }

            var mp = Tables[Table.MappedPasswords];
            MappedPasswords = new DataTable(mp.Name);
            MappedPasswords.Columns.Add(nameof(Column.Path), typeof(string));
            MappedPasswords.Columns.Add(nameof(Column.Password), typeof(string));
            MappedPasswords.PrimaryKey = new[] { MappedPasswords.Columns[nameof(Column.Path)] };
            if (File.Exists(mp.FullPath)) {
                try { MappedPasswords.ReadXml(mp.FullPath); }
                catch {
                    MessageBox.Show(GetRes(@"msg_ErrorLoadConfig", GetRes(@"ttl_MappedPasswords")), string.Empty, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
        }

        public static void SaveConfigToFile(string path = null) {
            if (path == null) path = FilePath;

            var fallbackPwds = "";
            if (FallbackPasswords != null && FallbackPasswords.Count > 0) {
                foreach (var s in FallbackPasswords) {
                    if (string.IsNullOrWhiteSpace(s.Item)) continue;
                    fallbackPwds += s.Item + "=\r\n";
                }
            }

            MappedPasswords?.WriteXml(Tables[Table.MappedPasswords].FullPath, XmlWriteMode.WriteSchema);

            File.WriteAllText(path, 
$@"[{nameof(ConfigSection.AppConfig)}]
{string.Join("\r\n", appConfigs.Select(p => $"{p.Name}={JsonConvert.SerializeObject(p.GetValue(null))}"))}

[{nameof(ConfigSection.CustomCommands)}]
{(CustomCommands?.Count > 0 ?
    string.Join("\r\n", CustomCommands.Select((oo, i) => $"{nameof(CustomCommands)}.{i}={oo.Str1}\t{oo.Str2}\t{oo.Str3}")) :
    null)}

;Fallback passwords for zipped files in the format:
;password=
[{nameof(ConfigSection.FallbackPasswords)}]
{fallbackPwds.Trim()}
");
        }
    }
}
