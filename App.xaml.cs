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
        public static MainWindow MainWin;
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
        public const int PreviewCount = 4;
        public static Random Random = new Random();

        internal static ImageSource fa_meh;
        internal static ImageSource fa_spinner;
        internal static ImageSource fa_exclamation;

        public static CubicEase CE_EaseIn => (CubicEase)Current.FindResource("CE_EaseIn");
        public static CubicEase CE_EaseOut => (CubicEase)Current.FindResource("CE_EaseOut");
        public static CubicEase CE_EaseInOut => (CubicEase)Current.FindResource("CE_EaseInOut");


        private void App_Startup(object sender, StartupEventArgs e) {
            Setting.LoadConfigFromFile();

            //create resources
            fa_meh = FontAwesome5.ImageAwesome.CreateImageSource(
                FontAwesome5.EFontAwesomeIcon.Solid_Meh,
                new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)));
            fa_spinner = FontAwesome5.ImageAwesome.CreateImageSource(
                FontAwesome5.EFontAwesomeIcon.Solid_Spinner,
                new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)));
            fa_exclamation = FontAwesome5.ImageAwesome.CreateImageSource(
                FontAwesome5.EFontAwesomeIcon.Solid_ExclamationCircle,
                new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)));

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

            //show mainwindow
            MainWin = new MainWindow() {
                Width = Setting.LastWindowSize.Width,
                Height = Setting.LastWindowSize.Height,
            };
            MainWin.Show();
        }


        private void App_Exit(object sender, ExitEventArgs e) {
            Setting.SaveConfigToFile();

            //db maintenance
            Execute(con => {
                using (var cmd = new SQLiteCommand(con)) {
                    //chech size limit on thumb db
                    if (Setting.ThumbDbSize < 10240) {
                        long pageCount, pageSize;
                        while (true) {
                            cmd.CommandText = $@"pragma page_count";
                            using (var reader = cmd.ExecuteReader()) { reader.Read(); pageCount = (long)reader["page_count"]; }
                            cmd.CommandText = $@"pragma page_size";
                            using (var reader = cmd.ExecuteReader()) { reader.Read(); pageSize = (long)reader["page_size"]; }
                            if (pageCount * pageSize < Setting.ThumbDbSize * 1048576) break;

                            cmd.CommandText = $@"delete from {Table_ThumbsData.Name} where rowid in
(select rowid from {Table_ThumbsData.Name} order by rowid asc limit 50)";
                            cmd.ExecuteNonQuery();
                            cmd.CommandText = "vacuum";
                            cmd.ExecuteNonQuery();
                        }
                    }

                    return 0;
                }
            });

        }
    }
}
