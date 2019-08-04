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
using Size = System.Drawing.Size;

namespace ZipImageViewer
{
    public class ImageInfo {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public ImageSource ImageSource { get; set; }
    }

    public partial class MainWindow : Window {
        private readonly string[] genericPasswords =
            App.config["Saved Passwords"].Where(d => d.Value.Length == 0).Select(d => d.KeyName).ToArray();

        public ObservableKeyedCollection<string, ImageInfo> ImageList { get; set; } =
            new ObservableKeyedCollection<string, ImageInfo>(ii => ii.FullPath);

        public MainWindow() {
            InitializeComponent();
        }

        private void MainWin_Loaded(object sender, RoutedEventArgs e)
        {
//            Task.Run(() => AddImages(new [] { @"D:\Source\Samples\JPEGAutoRotator-master\EXIF Test Files" }));
        }

        private void MainWin_Drop(object sender, DragEventArgs e)
        {
            ImageList.Clear();
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths == null) return;

            Task.Run(() => {
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
            });
        }

//        private void AddThumbnail(string filePath, ImageSource source) {
//            if (source == null) return;
//            Dispatcher.Invoke(() => {
//                var tn = new Thumbnail(filePath, source);
////                            tn.MouseUp += (s, e) => {
////                                if (e.ChangedButton == MouseButton.Left) {
////                                    new ViewWindow {ImagePath = file}.Show();
////                                }
////                            };
//                WP1.Children.Add(tn);
//            }, System.Windows.Threading.DispatcherPriority.Background);
//        }

        private void LoadFile(FileSystemInfo file) {
            var ft = Helpers.GetFileType(file.Name);
            if (ft == App.FileType.Image) {
                ImageList.Add(new ImageInfo {
                    FileName = file.Name,
                    FullPath = file.FullName,
                    ImageSource = Helpers.GetImageSource(file.FullName, App.ThumbnailSize.Width)
                });
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
                    //check if file is supported
                    var imgfile = ext.ArchiveFileData[i];
                    if (imgfile.IsDirectory || Helpers.GetFileType(imgfile.FileName) != App.FileType.Image) continue;
#if DEBUG
                    Console.WriteLine("Extracting " + ext.ArchiveFileData[i].FileName);
#endif
                    using (var ms = new MemoryStream()) {
                        ext.ExtractFile(i, ms);
                        ImageList.Add(new ImageInfo {
                            FileName = imgfile.FileName,
                            ImageSource = Helpers.GetImageSource(ms, App.ThumbnailSize.Width),
                        });
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


//        private void MainWin_PreviewMouseUp(object sender, MouseButtonEventArgs e) {
//            WP1.Children.Clear();
//            GC.Collect();
//        }
    }
}
