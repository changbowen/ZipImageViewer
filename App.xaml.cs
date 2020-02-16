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

namespace ZipImageViewer
{
    public partial class App : Application
    {
        public static readonly string ExeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        public static readonly HashSet<string> ImageExtensions =
            new HashSet<string>(new[] {
                "jpg", "jpeg", "png", "gif", "tiff", "bmp",
                ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".bmp",
            });
        public static readonly HashSet<string> ZipExtensions =
            new HashSet<string>(new[] {
                "zip", "rar", "7z",
                ".zip", ".rar", ".7z",
            });
        //public const int PreviewCount = 4;
        public static Random Random = new Random();

        internal static ImageSource fa_meh;
        internal static ImageSource fa_spinner;
        internal static ImageSource fa_exclamation;
        internal static ImageSource fa_file;
        internal static ImageSource fa_folder;
        internal static ImageSource fa_archive;
        internal static ImageSource fa_image;


        public static CubicEase CE_EaseIn => (CubicEase)Current.FindResource("CE_EaseIn");
        public static CubicEase CE_EaseOut => (CubicEase)Current.FindResource("CE_EaseOut");
        public static CubicEase CE_EaseInOut => (CubicEase)Current.FindResource("CE_EaseInOut");

        public static Setting Setting { get; } = new Setting();

        public static readonly int MaxLoadThreads = Environment.ProcessorCount;
        public static System.Threading.SemaphoreSlim LoadThrottle = new System.Threading.SemaphoreSlim(MaxLoadThreads);

        private void App_Startup(object sender, StartupEventArgs e) {
            try {
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

                //create thumb database if not exist and update columns if not correct
                var aff1 = Execute(
                    con => {
                        using (var cmd = new SQLiteCommand(con)) {
                            cmd.CommandText =
$@"create table if not exists [{Table_ThumbsData.Name}] (
[{Table_ThumbsData.Col_VirtualPath}] TEXT NOT NULL,
[{Table_ThumbsData.Col_DecodeWidth}] INTEGER,
[{Table_ThumbsData.Col_DecodeHeight}] INTEGER,
[{Table_ThumbsData.Col_ThumbData}] BLOB)";
                            return cmd.ExecuteNonQuery();
                        }
                    });

                //add columns if not exist
                if (aff1[0] != null && aff1[0].Equals(-1)) {//-1 means table already exists
                    Execute(con => {
                        using (var cmd = new SQLiteCommand(con)) {
                            cmd.CommandText =
                            $@"alter table [{Table_ThumbsData.Name}] add column [{Table_ThumbsData.Col_VirtualPath}] TEXT NOT NULL;";
                            try { cmd.ExecuteNonQuery(); } catch (SQLiteException) { }

                            cmd.CommandText =
                            $@"alter table [{Table_ThumbsData.Name}] add column [{Table_ThumbsData.Col_DecodeWidth}] INTEGER;";
                            try { cmd.ExecuteNonQuery(); } catch (SQLiteException) { }

                            cmd.CommandText =
                            $@"alter table [{Table_ThumbsData.Name}] add column [{Table_ThumbsData.Col_DecodeHeight}] INTEGER;";
                            try { cmd.ExecuteNonQuery(); } catch (SQLiteException) { }

                            cmd.CommandText =
                            $@"alter table [{Table_ThumbsData.Name}] add column [{Table_ThumbsData.Col_ThumbData}] BLOB;";
                            try { cmd.ExecuteNonQuery(); } catch (SQLiteException) { }
                        }
                        return 0;
                    });
                }

#if DEBUG
                Execute(con => {
                    using (var cmd = new SQLiteCommand(con)) {
                        cmd.CommandText = $@"delete from {Table_ThumbsData.Name}";
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = @"vacuum";
                        cmd.ExecuteNonQuery();
                    }
                    return 0;
                });
#endif

                //show mainwindow
                new MainWindow() {
                    Width = Setting.LastWindowSize.Width,
                    Height = Setting.LastWindowSize.Height,
                }.Show();
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "Application Start Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }


        private void App_Exit(object sender, ExitEventArgs e) {
            while (LoadThrottle.CurrentCount < MaxLoadThreads) {
                System.Threading.Thread.Sleep(100);
            }
            LoadThrottle.Dispose();

            Setting.SaveConfigToFile();

            //db maintenance
            Execute(con => {
                using (var cmd = new SQLiteCommand(con)) {
                    //chech size limit on thumb db
                    if (Setting.ThumbDbSize < 10d) {
                        long pageCount, pageSize, rowCount;
                        long targetSize = (long)(Setting.ThumbDbSize * 1073741824);
                        cmd.CommandText = $@"select count(*) from {Table_ThumbsData.Name}";
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
$@"delete from {Table_ThumbsData.Name} where rowid in
(select rowid from {Table_ThumbsData.Name} order by rowid asc limit {delCount})";
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
