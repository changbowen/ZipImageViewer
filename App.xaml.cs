using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using IniParser;
using IniParser.Model;
using SevenZip;
using Size = System.Drawing.Size;

namespace ZipImageViewer
{
    public partial class App : Application {
        public static MainWindow MainWin;
        public static IniData Config;
        public static readonly string[] ImageExtensions = {"jpg", "jpeg", "png", "gif", "tiff", "bmp"};
        public static readonly string[] ZipExtensions = {"zip", "rar", "7z"};
        public enum FileType { Unknown, Image, Archive }

        public static Size ThumbnailSize { get; set; } = new Size(300, 200);

        private void App_Startup(object sender, StartupEventArgs e) {
            //load config
            if (!File.Exists("config.ini")) File.WriteAllText("config.ini",
@"
[App Config]
SevenZipDllPath=C:\Program Files\7-Zip\7z.dll
ThumbnailSize=300x200

;Saved passwords for zipped files. Format:
;password=file_full_path
;leaving file_full_path empty indicates a password to try on all files when there is no matched path.
[Saved Passwords]
");
            var parser = new FileIniDataParser();
            Config = parser.ReadFile("config.ini");

            //dll path
            var sevenZipDllPath = Config["App Config"]["SevenZipDllPath"];
            if (string.IsNullOrEmpty(sevenZipDllPath)) {
                MessageBox.Show("7z.dll path is missing from the configuration.", "", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Current.Shutdown();
                return;
            }
            SevenZipBase.SetLibraryPath(sevenZipDllPath);

            //thumb size
            var thumbsize = Config["App Config"]["ThumbnailSize"]?.Split('x', '*', ',');
            if (thumbsize?.Length == 2)
                ThumbnailSize = new Size(int.Parse(thumbsize[0]), int.Parse(thumbsize[1]));

            //show mainwindow
            MainWin = new MainWindow();
            MainWin.Show();
        }


        private void App_Exit(object sender, ExitEventArgs e) {
            var parser = new FileIniDataParser();
            parser.WriteFile("config.ini", Config);
        }
    }
}
