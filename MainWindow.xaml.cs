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
    public partial class MainWindow : Window
    {
        private readonly string[] genericPasswords =
            App.Config["Saved Passwords"].Where(d => d.Value.Length == 0).Select(d => d.KeyName).ToArray();

        public ObservableKeyedCollection<string, ImageInfo> ImageList { get; set; } =
            new ObservableKeyedCollection<string, ImageInfo>(ii => ii.ImageRealPath);

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWin_Loaded(object sender, RoutedEventArgs e)
        {
#if DEBUG
            Task.Run(() => LoadFile(@"E:\Pictures\To Do.7z", ii => ImageList.Add(ii), App.ThumbnailSize.Width));
#endif
        }

        private void MainWin_Drop(object sender, DragEventArgs e)
        {
            ImageList.Clear();
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths == null) return;

            Task.Run(() =>
            {
                foreach (var path in paths)
                {
              // check if path is a file or directory
              var info = new DirectoryInfo(path);
                    if (info.Attributes.HasFlag(FileAttributes.Directory))
                    {
                        foreach (var file in info.EnumerateFiles("*.*", SearchOption.AllDirectories))
                        {
                            LoadFile(file.FullName, ii => ImageList.Add(ii), App.ThumbnailSize.Width);
                        }
                    }
                    else
                    {
                        LoadFile(info.FullName, ii => ImageList.Add(ii), App.ThumbnailSize.Width);
                    }
                }
            });
        }

        /// <summary>
        /// Load image based on the type of file and try passwords when possible.
        /// Can be called from a background thread.
        /// Callback can be used to handle the display of image. Use Dispatcher if callback needs to access the UI thread.
        /// </summary>
        public void LoadFile(string filePath, Action<ImageInfo> callback,
            int decodeWidth = 0, string[] extractedFileNames = null)
        {
            var fileName = Path.GetFileName(filePath);
            var ft = Helpers.GetFileType(fileName);
            if (ft == App.FileType.Image)
            {
                var imgInfo = new ImageInfo
                {
                    FileName = fileName,
                    FilePath = filePath,
                    FileType = App.FileType.Image,
                    ImageSource = Helpers.GetImageSource(filePath, decodeWidth)
                };
                callback(imgInfo);
            }
            else if (ft == App.FileType.Archive)
            {
                while (true)
                {
                    bool success;
                    //first check if there is a match in saved passwords
                    var pwd = App.Config["Saved Passwords"][filePath];
                    if (pwd != null)
                    {
                        //matching password found
                        success = extractZip(filePath, callback, decodeWidth, pwd, extractedFileNames); //return null when there extraction fails
                        if (success) break;
                    }

                    //then try no password
                    success = extractZip(filePath, callback, decodeWidth, null, extractedFileNames);
                    if (success) break;

                    //then try all saved passwords with no filename
                    foreach (var gp in genericPasswords)
                    {
                        success = extractZip(filePath, callback, decodeWidth, gp, extractedFileNames);
                        if (success) break;
                    }

                    //if all fails prompt for a generic or dedicated password
                    //then extract with it
                    //-------------logic needed here-------------


                    //exit loop
                    break;
                }
            }
        }

        private static bool extractZip(string path, Action<ImageInfo> callback,
                                       int decodeWidth = 0, string pwd = null, string[] extractedFileNames = null)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return extractZip(fs, path, callback, decodeWidth, pwd, extractedFileNames);
            }
        }

        private static bool extractZip(Stream stream, string path, Action<ImageInfo> callback,
                                       int decodeWidth = 0, string pwd = null, string[] extractedFileNames = null)
        {
            SevenZipExtractor ext = null;
            stream.Position = 0;
            try
            {
                ext = new SevenZipExtractor(stream, pwd);
                if (extractedFileNames == null || extractedFileNames.Length == 0) //extract all
                {
                    for (int i = 0; i < ext.ArchiveFileData.Count; i++)
                    {
                        //check if file is supported
                        var imgfile = ext.ArchiveFileData[i];
                        if (imgfile.IsDirectory || Helpers.GetFileType(imgfile.FileName) != App.FileType.Image)
                            continue;
#if DEBUG
                        Console.WriteLine("Extracting " + ext.ArchiveFileData[i].FileName);
#endif
                        var imgInfo = new ImageInfo
                        {
                            FilePath = path,
                            FileType = App.FileType.Archive,
                        };
                        using (var ms = new MemoryStream())
                        {
                            ext.ExtractFile(i, ms);
                            imgInfo.FileName = imgfile.FileName;
                            imgInfo.ImageSource = Helpers.GetImageSource(ms, decodeWidth);
                        }

                        callback(imgInfo);
                    }
                }
                else
                {
                    foreach (var fileName in extractedFileNames) //extract specified
                    {
                        var imgInfo = new ImageInfo
                        {
                            FilePath = path,
                            FileType = App.FileType.Archive,
                            FileName = fileName
                        };
                        using (var ms = new MemoryStream())
                        {
                            ext.ExtractFile(fileName, ms);
                            imgInfo.ImageSource = Helpers.GetImageSource(ms, decodeWidth);
                        }

                        callback(imgInfo);
                    }
                }

                //save password for the future
                App.Config["Saved Passwords"][path] = ext.Password;
                return true;
            }
            catch (Exception ex)
            {
                if (ex is ExtractionFailedException ||
                    ex is SevenZipArchiveException ||
                    ex is NotSupportedException) return false;
                throw;
            }
            finally
            {
                ext?.Dispose();
            }
        }


        //        private void MainWin_PreviewMouseUp(object sender, MouseButtonEventArgs e) {
        //            WP1.Children.Clear();
        //            GC.Collect();
        //        }
        private void TN1_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var tn = (Thumbnail)sender;
            var tnInfo = tn.ImageInfo;

            LoadFile(tnInfo.FilePath, ii => new ViewWindow { ImageInfo = ii }.Show(), 0, new[] { tnInfo.FileName });
        }
    }
}
