using FontAwesome5;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static ZipImageViewer.SQLiteHelper;
using static ZipImageViewer.TableHelper;
using static ZipImageViewer.Helpers;
using System.Net;
using System.Data;

namespace ZipImageViewer
{
    public partial class App : Application
    {
        public static readonly string ExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        public static readonly string ExeDir = Path.GetDirectoryName(ExePath);
        public static string BuildConfig { get {
#if DEBUG
                return "Debug";
#else
                return "Release";
#endif
        } }
        public static Version Version => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        public static readonly HashSet<string> ImageExtensions =
            new HashSet<string>(new[] {
                ".jpg", ".jpeg", ".png", ".tiff", ".tif", ".gif", ".bmp", ".ico", ".dds", ".jxr", ".hdp", ".wdp", ".heic", "heif"
            });
        public static readonly HashSet<string> ZipExtensions =
            new HashSet<string>(new[] {
                ".zip", ".rar", ".7z", ".bz2", ".bzip2", ".tbz2", ".tbz", ".gz", ".gzip", ".tgz", ".tar",
                ".wim", ".swm", ".esd", ".xz", ".txz", ".zipx", ".jar", ".xpi", ".odt", ".ods", ".docx",
                ".xlsx", ".epub", ".apm", ".ar", ".a", ".deb", ".lib", ".arj", ".cab", ".chm", ".chw",
                ".chi", ".chq", ".msi", ".msp", ".doc", ".xls", ".ppt", ".cpio", ".cramfs", ".dmg",
                ".ext", ".ext2", ".ext3", ".ext4", ".img", ".fat", ".img", ".hfs", ".hfsx", ".hxs",
                ".hxi", ".hxr", ".hxq", ".hxw", ".lit", ".ihex", ".iso", ".img", ".lzh", ".lha", ".lzma",
                ".mbr", ".mslz", ".mub", ".nsis", ".ntfs", ".img", ".mbr", ".r00", ".rpm", ".ppmd",
                ".qcow", ".qcow2", ".qcow2c", ".001", ".squashfs", ".udf", ".iso", ".img", ".scap",
                ".uefif", ".vdi", ".vhd", ".vmdk", ".xar", ".pkg", ".z", ".taz"
            });
        //public const int PreviewCount = 4;
        public static Random Random = new Random();

        public static ImageSource fa_meh { get; private set; }
        public static ImageSource fa_spinner { get; private set; }
        public static ImageSource fa_exclamation { get; private set; }
        public static ImageSource fa_file { get; private set; }
        public static ImageSource fa_folder { get; private set; }
        public static ImageSource fa_archive { get; private set; }
        public static ImageSource fa_image { get; private set; }


        public static CubicEase CE_EaseIn => (CubicEase)Current.FindResource("CE_EaseIn");
        public static CubicEase CE_EaseOut => (CubicEase)Current.FindResource("CE_EaseOut");
        public static CubicEase CE_EaseInOut => (CubicEase)Current.FindResource("CE_EaseInOut");

        public static Setting Setting { get; } = new Setting();

        public static ContextMenuWindow ContextMenuWin;

        private void App_Startup(object sender, StartupEventArgs e) {
            string initialPath = null;
            try {
                Init();

                //load config
                if (!Setting.LoadConfigs()) {
                    Current.Shutdown();
                    return;
                }

                //handle setting changes
                Setting.StaticPropertyChanged += Setting_StaticPropertyChanged;

                //check arguments
                if (e.Args?.Length > 0) {
#if DEBUG
                    if (e.Args.Contains("-cleandb")) {
                        Execute(Table.Thumbs, (table, con) => {
                            using (var cmd = new SQLiteCommand(con)) {
                                cmd.CommandText = $@"delete from {table.Name}";
                                cmd.ExecuteNonQuery();
                                cmd.CommandText = @"vacuum";
                                cmd.ExecuteNonQuery();
                            }
                            return 0;
                        });
                    }
#endif
                    //use the last arg as path
                    var path = e.Args[e.Args.Length - 1];
                    var objInfo = new ObjectInfo(path, GetPathType(path));
                    initialPath = objInfo.ContainerPath;

                    if (e.Args.Contains("-slideshow")) {
                        new SlideshowWindow(objInfo.ContainerPath).Show();
                        return;
                    }
                    else if (objInfo.Flags == FileFlags.Image) {
                        var viewWin = new ViewWindow(objInfo.ContainerPath, objInfo.FileName);
                        viewWin.Closing += (sender1, e1) => {
                            //load mainwindow
                            PostInit(initialPath);
                        };
                        viewWin.Show();
                        return;
                    }
                    else {
                        var mainWin = new MainWindow() { InitialPath = initialPath };
                        mainWin.Navigate += postInitOnce;
                        mainWin.Show();
                        return;
                    }
                }
            }
            catch (Exception ex) {
                MessageBox.Show(string.Join("\r\n", new[] { ex.Message, ex.InnerException?.Message }.Where(s => !string.IsNullOrEmpty(s))),
                    GetRes("ttl_AppStartError"), MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
            }

            PostInit(initialPath);
        }

        private void postInitOnce(object sender, (string Path, short? Direction) e)
        {
            if (sender is MainWindow mainWin) {
                PostInit(createMainWindow: false);
                mainWin.Navigate -= postInitOnce;
            }
        }

        private void Setting_StaticPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(Setting.ImmersionMode): {
                    foreach (var win in Windows) {
                        if (!(win is MainWindow mainWin)) continue;
                        Task.Run(() => mainWin.LoadPath(mainWin.CurrentPath));
                    }
                }
                break;
                case nameof(Setting.EncryptPasswords): {
                    if (Setting.EncryptPasswords == null) break;
                    if (Setting.FallbackPasswords == null || Setting.MappedPasswords == null) break;
                    var showMismatch = false;
                    if (Setting.EncryptPasswords == true) {//set password when option is enabled
                        for (int i = 0; i < 10; i++) {
                            var (answer, _, newPwd, cfmPwd) = InputWindow.PromptForPasswordChange(false, false, showMismatch);
                            if (!answer) { Setting.EncryptPasswords = null; return; }
                            showMismatch = false;
                            if (newPwd != cfmPwd) showMismatch = true;
                            else {
                                Setting.ChangeMasterPassword(newPwd);
                                break;
                            }
                        }
                    }
                    new BlockWindow(autoClose: true) {
                        MessageBody = GetRes("ttl_Processing_0", GetRes("ttl_SavedPasswords")),
                        Work = () => {
                            var enabled = Setting.EncryptPasswords == true;
                            foreach (DataRow row in Setting.FallbackPasswords.Rows) {
                                row[nameof(Column.Password)] = enabled ?
                                    EncryptionHelper.TryEncrypt(row[nameof(Column.Password)].ToStr()).Output :
                                    EncryptionHelper.TryDecrypt(row[nameof(Column.Password)].ToStr()).Output;
                            }
                            foreach (DataRow row in Setting.MappedPasswords.Rows) {
                                row[nameof(Column.Password)] = enabled ?
                                    EncryptionHelper.TryEncrypt(row[nameof(Column.Password)].ToStr()).Output :
                                    EncryptionHelper.TryDecrypt(row[nameof(Column.Password)].ToStr()).Output;
                            }
                            Setting.SaveConfigs();
                        }
                    }.ShowDialog();
                }
                break;
            }
        }

        private void App_Exit(object sender, ExitEventArgs e) {
            while (LoadHelper.LoadThrottle.CurrentCount < LoadHelper.MaxLoadThreads) {
                System.Threading.Thread.Sleep(100);
            }
            LoadHelper.LoadThrottle.Dispose();

            Setting.SaveConfigs();

            Setting.StaticPropertyChanged -= Setting_StaticPropertyChanged;

            //db maintenance
            Execute(Table.Thumbs, (table, con) => {
                using (var cmd = new SQLiteCommand(con)) {
                    //chech size limit on thumb db
                    if (Setting.ThumbDbSize < 10d) {
                        long pageCount, pageSize, rowCount;
                        long targetSize = (long)(Setting.ThumbDbSize * 1073741824);
                        cmd.CommandText = $@"select count(*) from {table.Name}";
                        using (var reader = cmd.ExecuteReader()) { reader.Read(); rowCount = (long)reader["count(*)"]; }

                        while (true) {
                            cmd.CommandText = @"pragma page_count";
                            using (var reader = cmd.ExecuteReader()) { reader.Read(); pageCount = (long)reader["page_count"]; }
                            cmd.CommandText = @"pragma page_size";
                            using (var reader = cmd.ExecuteReader()) { reader.Read(); pageSize = (long)reader["page_size"]; }

                            var dbSize = pageCount * pageSize;
                            if (dbSize < targetSize) break;

                            //calculate how many rows to delete
                            var delCount = (dbSize - targetSize) / (dbSize / rowCount) * 2;
                            if (delCount < 50) delCount = 50L;

                            cmd.CommandText =
$@"delete from {table.Name} where rowid in
(select rowid from {table.Name} order by rowid asc limit {delCount})";
                            cmd.ExecuteNonQuery();
                            cmd.CommandText = @"vacuum";
                            cmd.ExecuteNonQuery();
                        }
                    }

                    return 0;
                }
            });

        }

        private void Init()
        {
            //localization
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            if (culture.TwoLetterISOLanguageName != @"en") {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = assembly.GetName().Name + ".g";
                var resourceManager = new System.Resources.ResourceManager(resourceName, assembly);
                try {
                    var resourceSet = resourceManager.GetResourceSet(culture, true, true);
                    if (resourceSet.Cast<System.Collections.DictionaryEntry>()
                        .Any(entry => (string)entry.Key == $@"resources/localization.{culture.TwoLetterISOLanguageName}.baml")) {
                        Resources.MergedDictionaries.Add(new ResourceDictionary {
                            Source = new Uri($@"Resources\Localization.{culture.TwoLetterISOLanguageName}.xaml", UriKind.Relative)
                        });
                    }
                }
                finally {
                    resourceManager.ReleaseAllResources();
                }
            }

            //get supported extensions
            foreach (var ext in RegistryHelpers.GetWICDecoders().Select(s => s.ToLowerInvariant())) {
                if (!ImageExtensions.Contains(ext)) ImageExtensions.Add(ext);
            }

            //set working directory
            Directory.SetCurrentDirectory(ExeDir);

            //create resources
            var fa_brush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            fa_meh = GetFaIcon(EFontAwesomeIcon.Solid_Meh, fa_brush);
            fa_spinner = GetFaIcon(EFontAwesomeIcon.Solid_Spinner, fa_brush);
            fa_exclamation = GetFaIcon(EFontAwesomeIcon.Solid_ExclamationCircle, fa_brush);
            fa_file = GetFaIcon(EFontAwesomeIcon.Solid_File, fa_brush);
            fa_folder = GetFaIcon(EFontAwesomeIcon.Solid_Folder, fa_brush);
            fa_archive = GetFaIcon(EFontAwesomeIcon.Solid_FileArchive, fa_brush);
            fa_image = GetFaIcon(EFontAwesomeIcon.Solid_FileImage, fa_brush);
        }

        private void PostInit(string initialPath = null, bool createMainWindow = true, bool checkDB = true, bool checkUpdate = true)
        {
            if (!createMainWindow && !checkDB && !checkUpdate) return;

            //normal start and load MainWindow
            var bw = new BlockWindow(autoClose: true) { MessageTitle = $"{GetRes("ttl_AppStarting")}..." };
            bw.TB_Message.HorizontalAlignment = HorizontalAlignment.Center;
            bw.TB_Message.VerticalAlignment = VerticalAlignment.Center;
            bw.Work = () => {
                try {
                    if (checkDB) {
                        //make sure thumbs db is correct
                        Dispatcher.Invoke(() => bw.MessageBody = $"{GetRes("ttl_Checking")} database...");
                        CheckThumbsDB();
                    }
                    
                    if (createMainWindow) Dispatcher.Invoke(() => {
                        //show mainwindow if no cmdline args
                        new MainWindow() { InitialPath = initialPath }.Show();
                    });
                    

                    if (checkUpdate) Task.Run(() => {
                        //check for updates
                        var localVer = Version;
                        var req = (HttpWebRequest)WebRequest.Create(@"https://api.github.com/repos/changbowen/zipimageviewer/releases/latest");
                        req.ContentType = @"application/json; charset=utf-8";
                        req.UserAgent = nameof(ZipImageViewer);
                        try {
                            using (var res = req.GetResponse() as HttpWebResponse)
                            using (var stream = res.GetResponseStream())
                            using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8)) {
                                var jObj = Newtonsoft.Json.Linq.JObject.Parse(reader.ReadToEnd());
                                string tag_name = @"tag_name";
                                if (!jObj.ContainsKey(tag_name)) return;
                                var remoteVer = Version.Parse(jObj[tag_name].ToString().TrimStart('v'));
                                if (localVer < remoteVer && MessageBox.Show(GetRes(@"msg_NewVersionPrompt", localVer.ToString(3), remoteVer.ToString(3)), string.Empty,
                                    MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK) {
                                    Helpers.Run(@"explorer", @"https://github.com/changbowen/ZipImageViewer/releases");
                                }
                            }
                        }
                        catch { }
                    });
                }
                catch (Exception ex) {
                    MessageBox.Show(string.Join("\r\n", new[] { ex.Message, ex.InnerException?.Message }.Where(s => !string.IsNullOrEmpty(s))),
                        GetRes("ttl_AppStartError"), MessageBoxButton.OK, MessageBoxImage.Error);
                    Dispatcher.Invoke(Current.Shutdown);
                }
            };

            bw.Show();
        }
    }
}
