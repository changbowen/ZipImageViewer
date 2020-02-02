using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;



namespace ZipImageViewer
{
    public partial class App : Application {
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


        private void App_Startup(object sender, StartupEventArgs e) {
            Setting.LoadConfigFromFile();
            //show mainwindow
            MainWin = new MainWindow();
            MainWin.Show();
        }


        private void App_Exit(object sender, ExitEventArgs e) {
            Setting.SaveConfigToFile();
        }
    }
}
