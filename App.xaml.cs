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

namespace ZipImageViewer
{
    public partial class App : Application
    {
        public static readonly string ExeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        public static readonly HashSet<string> ImageExtensions =
            new HashSet<string>(new[] {
                ".jpg", ".jpeg", ".png", ".tiff", ".gif", ".bmp", ".ico", ".dds", ".jxr", ".hdp", ".wdp"
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
            try {
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

                //handle immersion mode change
                Setting.StaticPropertyChanged += Setting_StaticPropertyChanged;

                //get supported extensions
                foreach (var ext in NativeHelpers.GetWICDecoders().Select(s => s.ToLowerInvariant())) {
                    if (!ImageExtensions.Contains(ext)) ImageExtensions.Add(ext);
                }

                //set working directory
                Directory.SetCurrentDirectory(ExeDir);

                //load config
                Setting.LoadConfigFromFile();

                //create resources
                var fa_brush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                fa_meh = ImageAwesome.CreateImageSource(EFontAwesomeIcon.Solid_Meh, fa_brush);
                fa_spinner = ImageAwesome.CreateImageSource(EFontAwesomeIcon.Solid_Spinner, fa_brush);
                fa_exclamation = ImageAwesome.CreateImageSource(EFontAwesomeIcon.Solid_ExclamationCircle, fa_brush);
                fa_file = ImageAwesome.CreateImageSource(EFontAwesomeIcon.Solid_File, fa_brush);
                fa_folder = ImageAwesome.CreateImageSource(EFontAwesomeIcon.Solid_Folder, fa_brush);
                fa_archive = ImageAwesome.CreateImageSource(EFontAwesomeIcon.Solid_FileArchive, fa_brush);
                fa_image = ImageAwesome.CreateImageSource(EFontAwesomeIcon.Solid_FileImage, fa_brush);

                //make sure thumbs db is correct
                CheckThumbsDB();

                //check args
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
                    try {
                        //use the last arg as path
                        var path = e.Args[e.Args.Length - 1];
                        var objInfo = new ObjectInfo(path, GetPathType(path));

                        if (e.Args.Contains("-slideshow")) {
                            new SlideshowWindow(objInfo.ContainerPath).Show();
                            return;
                        }
                        else {
                            switch (objInfo.Flags) {
                                case FileFlags.Image:
                                    new ViewWindow(objInfo.ContainerPath, objInfo.FileName).Show();
                                    return;
                                default:
                                    new MainWindow() { InitialPath = objInfo.ContainerPath }.Show();
                                    return;
                            }
                        }
                    }
                    catch (Exception ex) {
                        MessageBox.Show(ex.Message, GetRes("ttl_ParamStartError"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                //show mainwindow if no cmdline args
                new MainWindow().Show();
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, GetRes("ttl_AppStartError"), MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
            }
        }

        private void Setting_StaticPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(Setting.ImmersionMode)) {
                foreach (var win in Windows) {
                    if (!(win is MainWindow mainWin)) continue;
                    Task.Run(() => mainWin.LoadPath(mainWin.CurrentPath));
                }
            }
        }

        private void App_Exit(object sender, ExitEventArgs e) {
            while (LoadHelper.LoadThrottle.CurrentCount < LoadHelper.MaxLoadThreads) {
                System.Threading.Thread.Sleep(100);
            }
            LoadHelper.LoadThrottle.Dispose();

            Setting.SaveConfigToFile();

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
    }
}
