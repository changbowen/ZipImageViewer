using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

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
        public static Random Random = new Random();

        internal static ImageSource fa_meh;
        internal static ImageSource fa_spinner;
        internal static ImageSource fa_exclamation;


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

            //show mainwindow
            MainWin = new MainWindow();
            MainWin.Show();
        }


        private void App_Exit(object sender, ExitEventArgs e) {
            Setting.SaveConfigToFile();
        }
    }
}
