using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using IniParser;
using SevenZip;
using static ZipImageViewer.Helpers;
using static ZipImageViewer.TableHelper;
using static ZipImageViewer.SQLiteHelper;
using static ZipImageViewer.SlideshowHelper;
using System.Reflection;
using Newtonsoft.Json;
using System.Text;

namespace ZipImageViewer
{
    public class PropertyChangedWithValueEventArgs : PropertyChangedEventArgs
    {
        public virtual object OldValue { get; private set; }
        public virtual object NewValue { get; private set; }

        public PropertyChangedWithValueEventArgs(string propName, object oldValue, object newValue) : base(propName) {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    public class Setting : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(string propName, object oldValue = null, object newValue = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedWithValueEventArgs(propName, oldValue, newValue));
        }

        public static event EventHandler<PropertyChangedEventArgs> StaticPropertyChanged;
        protected static void OnStaticPropertyChanged(string propName, object oldValue = null, object newValue = null) {
            StaticPropertyChanged?.Invoke(null, new PropertyChangedWithValueEventArgs(propName, oldValue, newValue));
        }

        [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
        sealed class AppConfigAttribute : Attribute { }

        public enum Transition
        {
            [Description("ttl_" + nameof(None), true)]
            None,
            [Description("ttl_" + nameof(Random), true)]
            Random,
            [Description("ttl_" + nameof(ZoomFade), true)]
            ZoomFade,
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
        public enum ThumbnailFormats
        {
            [Description("JPEG")]
            Jpeg,
            [Description("PNG")]
            Png
        }

        private enum ConfigSection
        { AppConfig, CustomCommands, FallbackPasswords }

        public static string FilePath => Path.Combine(App.ExeDir, @"config.ini");

        private static string sevenZipDllPath => Path.Combine(App.ExeDir, @"7z.dll");


        private static string databaseDir = @".\";
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

        private static ThumbnailFormats thumbnailFormat = ThumbnailFormats.Jpeg;
        [AppConfig]
        public static ThumbnailFormats ThumbnailFormat {
            get => thumbnailFormat;
            set {
                if (thumbnailFormat == value) return;
                thumbnailFormat = value;
                OnStaticPropertyChanged(nameof(ThumbnailFormat));
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

        private static bool? encryptPasswords = false;
        /// <summary>
        /// Setting to true or false will trigger encryption or decryption of all saved passwords. Setting to null will not trigger.
        /// </summary>
        [AppConfig]
        public static bool? EncryptPasswords {
            get => encryptPasswords;
            set {
                if (encryptPasswords == value) return;
                encryptPasswords = value;
                OnStaticPropertyChanged(nameof(EncryptPasswords));
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

        private static DataTable fallbackPasswords;
        public static DataTable FallbackPasswords {
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

        [AppConfig]
        public static string MasterPasswordHash { get; private set; }

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
            get => RegistryHelpers.CheckExplorerMenuItem(@"*", @"Directory");
            set {
                if (value)
                    RegistryHelpers.SetExplorerMenuItem(@"*", @"Directory");
                else
                    RegistryHelpers.ClearExplorerMenuItem(@"*", @"Directory");

                OnStaticPropertyChanged(nameof(ExpMenuSlideshow));
            }
        }

        public static Rect LastViewWindowRect;


        private static string masterPassword = string.Empty;
        public static string MasterPassword => string.IsNullOrEmpty(masterPassword) ? string.Empty : Encoding.UTF8.GetString(Convert.FromBase64String(masterPassword));
        
        #endregion

        /// <summary>
        /// Set or change master password and update <see cref="MasterPasswordHash"/>. When <paramref name="oldPwd"/> is not null, re-encrypt all saved passwords.
        /// </summary>
        public static void ChangeMasterPassword(string newPwd, string oldPwd = null) {
            if (newPwd == null) return;

            masterPassword = Convert.ToBase64String(Encoding.UTF8.GetBytes(newPwd));
            MasterPasswordHash = EncryptionHelper.GetHash(newPwd);

            if (oldPwd != null) {//re-encrypt all passwords
                new BlockWindow(autoClose: true) {
                    MessageBody = GetRes("ttl_Processing_0", GetRes("ttl_SavedPasswords")),
                    Work = () => {
                        foreach (DataRow row in FallbackPasswords.Rows) {
                            row[nameof(Column.Password)] = EncryptionHelper.TryReEncrypt(row[nameof(Column.Password)].ToStr(), oldPwd, newPwd).Output;
                        }
                        foreach (DataRow row in MappedPasswords.Rows) {
                            row[nameof(Column.Password)] = EncryptionHelper.TryReEncrypt(row[nameof(Column.Password)].ToStr(), oldPwd, newPwd).Output;
                        }
                        SaveConfigs();
                    }
                }.ShowDialog();
            }
        }


        private static IEnumerable<PropertyInfo> appConfigs => typeof(Setting).GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.GetCustomAttributes(typeof(AppConfigAttribute), false).Length > 0);

        /// <summary>
        /// Returns true if load is considered successful.
        /// </summary>
        public static bool LoadConfigs() {
            //load config
            if (!File.Exists(FilePath)) {
                //initialize default config
                SaveConfigs();
            }

            //apply dll path
            SevenZipBase.SetLibraryPath(sevenZipDllPath);

            //parse config file
            var iniData = new FileIniDataParser().ReadFile(FilePath, Encoding.UTF8);
            foreach (var prop in appConfigs) {
                var saved = iniData[nameof(ConfigSection.AppConfig)][prop.Name];
                if (saved == null) continue;
                try { prop.SetValue(null, JsonConvert.DeserializeObject(saved, prop.PropertyType)); }
                catch {
                    MessageBox.Show(GetRes(@"msg_ErrorLoadConfig", prop.Name), string.Empty, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }

            //ask for master password if enabled
            if (EncryptPasswords == true) {
                for (int i = 0; i < 10; i++) {
                    var (answer, masterPwd) = App.Current.Dispatcher.Invoke(() => InputWindow.PromptForMasterPassword(i != 0));
                    if (!answer) return false;
                    if (EncryptionHelper.GetHash(masterPwd) == MasterPasswordHash) {
                        ChangeMasterPassword(masterPwd);
                        break;
                    }
                    if (i >= 9) return false;
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
            var fp = Tables[Table.FallbackPasswords];
            FallbackPasswords = new DataTable(fp.Name);//ObservableKeyedCollection<string, Observable<string>>(o => o.Item);
            FallbackPasswords.Columns.Add(nameof(Column.PasswordHash), typeof(string));
            FallbackPasswords.Columns.Add(nameof(Column.Password), typeof(string));
            FallbackPasswords.PrimaryKey = new[] { FallbackPasswords.Columns[nameof(Column.PasswordHash)] };
            if (File.Exists(fp.FullPath)) {
                try { FallbackPasswords.ReadXml(fp.FullPath); }
                catch (Exception ex) {
                    MessageBox.Show(GetRes(@"msg_ErrorLoadConfig", GetRes(@"ttl_FallbackPasswords")) + "\r\n" + ex.Message, string.Empty, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
            //load fallback passwords from ini if exist, then remove them
            if (iniData.Sections.ContainsSection(nameof(ConfigSection.FallbackPasswords))) {
                try {
                    iniData[nameof(ConfigSection.FallbackPasswords)]
                        .Where(d => d.Value.Length == 0)
                        .Select(d => new Observable<string>(d.KeyName)).ToList()
                        .ForEach(p => {
                            var encPwd = new EncryptionHelper.Password(p);
                            FallbackPasswords.UpdateDataTable(encPwd.Hash, nameof(Column.Password), encPwd.SafeValue);
                        });
                    iniData.Sections.RemoveSection(nameof(ConfigSection.FallbackPasswords));
                }
                catch (Exception ex) {
                    MessageBox.Show(GetRes(@"msg_ErrorLoadConfig", GetRes(@"ttl_FallbackPasswords")) + "\r\n" + ex.Message, string.Empty, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
            FallbackPasswords.ColumnChanging += UpdatePasswordHash;

            var mp = Tables[Table.MappedPasswords];
            MappedPasswords = new DataTable(mp.Name);
            MappedPasswords.Columns.Add(nameof(Column.Path), typeof(string));
            MappedPasswords.Columns.Add(nameof(Column.Password), typeof(string));
            MappedPasswords.PrimaryKey = new[] { MappedPasswords.Columns[nameof(Column.Path)] };
            if (File.Exists(mp.FullPath)) {
                try { MappedPasswords.ReadXml(mp.FullPath); }
                catch (Exception ex) {
                    MessageBox.Show(GetRes(@"msg_ErrorLoadConfig", GetRes(@"ttl_MappedPasswords")) + "\r\n" + ex.Message, string.Empty, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }

            return true;
        }

        public static void SaveConfigs() {
            FallbackPasswords?.WriteXml(Tables[Table.FallbackPasswords].FullPath, XmlWriteMode.WriteSchema);
            MappedPasswords?.WriteXml(Tables[Table.MappedPasswords].FullPath, XmlWriteMode.WriteSchema);

            File.WriteAllText(FilePath, 
$@"[{nameof(ConfigSection.AppConfig)}]
{string.Join("\r\n", appConfigs.Select(p => $"{p.Name}={JsonConvert.SerializeObject(p.GetValue(null))}"))}

[{nameof(ConfigSection.CustomCommands)}]
{(CustomCommands?.Count > 0 ?
    string.Join("\r\n", CustomCommands.Select((oo, i) => $"{nameof(CustomCommands)}.{i}={oo.Str1}\t{oo.Str2}\t{oo.Str3}")) :
    null)}

;(Deprecated) Fallback passwords for zipped files in the format:
;password=
");
        }
    }
}
