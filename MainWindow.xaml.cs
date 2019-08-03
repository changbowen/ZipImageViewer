using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using IniParser.Model;
using SevenZip;
using Path = System.IO.Path;

namespace ZipImageViewer
{
    public partial class MainWindow : Window {
        private readonly int decodePixelWidth = 100;
        private readonly string[] genericPasswords =
            App.config["Saved Passwords"].Where(d => d.Value.Length == 0).Select(d => d.KeyName).ToArray();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWin_Loaded(object sender, RoutedEventArgs e)
        {
            SevenZipBase.SetLibraryPath(@"C:\Program Files\7-Zip\7z.dll");
//            Task.Run(() => AddImages(new [] { @"D:\Source\Samples\JPEGAutoRotator-master\EXIF Test Files" }));
        }

        private void MainWin_Drop(object sender, DragEventArgs e)
        {
            WP1.Children.Clear();
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            Task.Run(() => AddThumbnails(paths));
        }

        private void AddThumbnails(string[] paths)
        {
            foreach (var path in paths)
            {
                // check if path is a file or directory
                var info = new DirectoryInfo(path);
                if (info.Attributes.HasFlag(FileAttributes.Directory))
                {
                    foreach (var file in info.EnumerateFiles("*.*", SearchOption.AllDirectories)) {
                        LoadFile(file);
                    }
                }
                else
                    LoadFile(info);
            }
        }

        private void AddThumbnail(BitmapSource source) {
            if (source == null) return;
            Dispatcher.Invoke(() => {
                var tn = new Thumbnail(source);
//                            tn.MouseUp += (s, e) => {
//                                if (e.ChangedButton == MouseButton.Left) {
//                                    new ViewWindow {ImagePath = file}.Show();
//                                }
//                            };
                WP1.Children.Add(tn);
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void LoadFile(FileSystemInfo file) {
            var extension = file.Extension.TrimStart('.').ToLowerInvariant();
            if (extension.Length == 0) return;

            App.FileType ft;
            if (App.ImageExtensions.Contains(extension)) ft = App.FileType.Image;
            else if (App.ZipExtensions.Contains(extension)) ft = App.FileType.Archive;
            else return;

            if (ft == App.FileType.Image) {
                AddThumbnail(GetImageSource(file.FullName, decodePixelWidth));
            }
            else if (ft == App.FileType.Archive) {
                while (true) {
                    bool success;
                    //first check if there is a match in saved passwords
                    var p = App.config["Saved Passwords"][file.Name];
                    if (p != null) {//matching password found
                        success = extractZip(file.FullName, p); //return false when there extraction fails
                        if (success) break;
                    }

                    //then try no password
                    success = extractZip(file.FullName);
                    if (success) break;

                    //then try all saved passwords with no filename
                    foreach (var gp in genericPasswords) {
                        success = extractZip(file.FullName, gp);
                        if (success) break;
                    }

                    //if all fails prompt for a generic or dedicated password
                    //then extract with it
                    break;
                }
            }
        }

        private bool extractZip(string path, string pwd = null) {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read)) {
                return extractZip(fs, pwd);
            }
        }

        private bool extractZip(Stream stream, string pwd = null) {
            SevenZipExtractor ext = null;
            stream.Position = 0;
            try {
                ext = new SevenZipExtractor(stream, pwd);
                for (int i = 0; i < ext.ArchiveFileData.Count; i++) {
                    if (ext.ArchiveFileData[i].IsDirectory) continue;

                    using (var ms = new MemoryStream()) {
                        ext.ExtractFile(i, ms);
                        AddThumbnail(GetImageSource(ms, decodePixelWidth));
                    }
                }
                return true;
            }
            catch (ExtractionFailedException) {
                return false;
            }
            finally {
                ext?.Dispose();
            }
        }

        private static BitmapSource GetImageSource(string path, int decodeWidth = 0) {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read)) {
                return GetImageSource(fs, decodeWidth);
            }
        }

        private static BitmapSource GetImageSource(Stream stream, int decodeWidth = 0) {
            stream.Position = 0;
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            ushort orien = 0;
            if ((frame.Metadata as BitmapMetadata)?.GetQuery("/app1/ifd/{ushort=274}") is ushort u)
                orien = u;
            frame = null;
//            var size = new Size(frame.PixelWidth, frame.PixelHeight);

            stream.Position = 0;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.DecodePixelWidth = decodeWidth;
            bi.StreamSource = stream;
            bi.EndInit();
            bi.Freeze();

            if (orien < 2) return bi;

            var tb = new TransformedBitmap();
            tb.BeginInit();
            tb.Source = bi;
            switch (orien) {
//              case 1:
//                  break;
                case 2:
                    tb.Transform = new ScaleTransform(-1d, 1d);
                    break;
                case 3:
                    tb.Transform = new RotateTransform(180d);
                    break;
                case 4:
                    tb.Transform = new ScaleTransform(1d, -1d);
                    break;
                case 5: {
                    var tg = new TransformGroup();
                    tg.Children.Add(new RotateTransform(90d));
                    tg.Children.Add(new ScaleTransform(-1d, 1d));
                    tb.Transform = tg;
                    break;
                }
                case 6:
                    tb.Transform = new RotateTransform(90d);
                    break;
                case 7:
                {
                    var tg = new TransformGroup();
                    tg.Children.Add(new RotateTransform(90d));
                    tg.Children.Add(new ScaleTransform(1d, -1d));
                    tb.Transform = tg;
                    break;
                }
                case 8:
                    tb.Transform = new RotateTransform(270d);
                    break;
            }
            tb.EndInit();
            tb.Freeze();
            return tb;
        }

        private void MainWin_PreviewMouseUp(object sender, MouseButtonEventArgs e) {
            WP1.Children.Clear();
            GC.Collect();
        }
    }
}
