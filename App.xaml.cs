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

namespace ZipImageViewer
{
    public partial class App : Application
    {
        public static IniData config;
        public static readonly string[] ImageExtensions = {"jpg", "jpeg", "png", "gif", "tiff", "bmp"};
        public static readonly string[] ZipExtensions = {"zip", "rar", "7z"};
        public enum FileType { Unknown, Image, Archive }


        private void App_Startup(object sender, StartupEventArgs e) {
            //load config
            if (!File.Exists("config.ini")) File.WriteAllText("config.ini",
@"[Saved Passwords]
;Saved passwords for zipped files. Format:
;password=file_full_path
;leaving file_full_path empty indicates a password to try on all files when there is no matched path.
");
            var parser = new FileIniDataParser();

            config = parser.ReadFile("config.ini");

            //show mainwindow
            new MainWindow().Show();
        }
    }
}
