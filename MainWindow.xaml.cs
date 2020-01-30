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
using Image = System.Drawing.Image;
using Path = System.IO.Path;
using Size = System.Drawing.Size;

namespace ZipImageViewer
{
    public partial class MainWindow : Window {
        public ObservableKeyedCollection<string, ImageInfo> ImageList { get; set; } =
            new ObservableKeyedCollection<string, ImageInfo>(ii => ii.ImageRealPath);

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWin_Loaded(object sender, RoutedEventArgs e)
        {
#if DEBUG
            //Task.Run(() => LoadFolder(@"E:\Pictures\new folder\", Callback_AddToImageList, new LoadOptions(Setting.ThumbnailSize.Width)));
            new SettingsWindow().ShowDialog();
#endif
            if (Helpers.OpenFolderDialog(this) is string path)
                Task.Run(() => LoadPath(path));
        }

        private void MainWin_Drop(object sender, DragEventArgs e)
        {
            ImageList.Clear();
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths == null || paths.Length == 0) return;

            Task.Run(() => {
                LoadPath(paths[0]);
            });
        }

        public void LoadPath(string path) {
            LoadPath(new DirectoryInfo(path));
        }
        public void LoadPath(FileSystemInfo info) {
            var pathType = Helpers.GetPathType(info);
            // check if path is a file or directory
            switch (pathType) {
                case FileFlags.Directory:
                    //directory
                    ImageList.Clear();
                    LoadFolder((DirectoryInfo)info);
                    break;
                default:
                    //file
                    LoadFile(info.FullName, pathType, new LoadOptions(Setting.ThumbnailSize.Width, callback: Callback_AddToImageList));
                    break;
            }
        }


        public void LoadFolder(DirectoryInfo dirInfo) {
            foreach (var childInfo in dirInfo.EnumerateFileSystemInfos()) {
                if (childInfo is DirectoryInfo childDirInfo) {
                    //get first image file
                    FileSystemInfo firstImg = null;
                    foreach (var fsInfo in childDirInfo.EnumerateFileSystemInfos()) {
                        var fType = Helpers.GetPathType(fsInfo);
                        if (fType != FileFlags.Image) continue;
                        firstImg = fsInfo;
                        break;
                    }
                    if (firstImg != null)
                        LoadFile(firstImg.FullName, FileFlags.Image | FileFlags.Directory,
                            new LoadOptions(Setting.ThumbnailSize.Width, callback: Callback_AddToImageList));
                }
                else {
                    var fileType = Helpers.GetPathType(childInfo);
                    switch (fileType) {
                        case FileFlags.Archive:
                            LoadFile(childInfo.FullName, fileType,
                                new LoadOptions(Setting.ThumbnailSize.Width, extractCount: 1, callback: Callback_AddToImageList));
                            break;
                        case FileFlags.Image:
                            LoadFile(childInfo.FullName, fileType,
                                new LoadOptions(Setting.ThumbnailSize.Width, callback: Callback_AddToImageList));
                            break;
                    }
                }
                
            }
        }

//        private void Callback_ViewImage(ImageInfo imgInfo, ViewWindow window) {
//            window.ImageInfo = imgInfo;
//            if (!window.IsLoaded) window.Show();
//        }

        private void Callback_AddToImageList(ImageInfo imgInfo) {
            ImageList.Add(imgInfo);
        }

        /// <summary>
        /// Load image based on the type of file and try passwords when possible.
        /// Can be called from a background thread.
        /// Callback can be used to handle the display of image. Use Dispatcher if callback needs to access the UI thread.
        /// <param name="flags">Only checks for Image and Archive.</param>
        /// </summary>
        public void LoadFile(string filePath, FileFlags flags, LoadOptions options = null)
        {
            if (options == null) options = new LoadOptions();

            var fileName = Path.GetFileName(filePath);
            if (flags.HasFlag(FileFlags.Image) && !flags.HasFlag(FileFlags.Archive))
            {
                var imgInfo = new ImageInfo
                {
                    FileName = fileName,
                    FilePath = filePath,
                    Flags = flags,
                    ImageSource = Helpers.GetImageSource(filePath, options.DecodeWidth)
                };
                options.Callback?.Invoke(imgInfo);
            }
            else if (flags.HasFlag(FileFlags.Archive)) {
                while (true)
                {
                    //first check if there is a match in saved passwords
                    var pwd = Setting.MappedPasswords[filePath];
                    if (pwd != null)
                    {
                        //matching password found
                        options.Password = pwd;
                        if (extractZip(filePath, options)) break;
                    }

                    //then try no password
                    options.Password = null;
                    if (extractZip(filePath, options)) break;

                    //then try all saved passwords with no filename
                    foreach (var fp in Setting.FallbackPasswords) {
                        options.Password = fp;
                        if (extractZip(filePath, options)) break;
                    }

                    //if all fails prompt for a generic or dedicated password
                    //then extract with it
                    //-------------logic needed here-------------


                    //exit loop
                    break;
                }
            }
        }

        /// <summary>
        /// Returns true or false based on whether extraction succeeds.
        /// </summary>
        private static bool extractZip(string path, LoadOptions options)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return extractZip(fs, path, options);
            }
        }

        /// <summary>
        /// Returns true or false based on whether extraction succeeds.
        /// </summary>
        private static bool extractZip(Stream stream, string path, LoadOptions options)
        {
            SevenZipExtractor ext = null;
            stream.Position = 0;
            try
            {
                ext = new SevenZipExtractor(stream, options.Password);
                if (options.FileNames == null || options.FileNames.Length == 0)
                {
                    //extract all
                    int extractCount = 0;
                    for (int i = 0; i < ext.ArchiveFileData.Count; i++)
                    {
                        //check if file is supported
                        var imgfile = ext.ArchiveFileData[i];
                        if (imgfile.IsDirectory || Helpers.GetPathType(imgfile.FileName) != FileFlags.Image)
                            continue;
#if DEBUG
                        Console.WriteLine("Extracting " + ext.ArchiveFileData[i].FileName);
#endif
                        var imgInfo = new ImageInfo
                        {
                            FilePath = path,
                            Flags = FileFlags.Archive | FileFlags.Image,
                        };
                        using (var ms = new MemoryStream())
                        {
                            ext.ExtractFile(i, ms);
                            imgInfo.FileName = imgfile.FileName;
                            imgInfo.ImageSource = Helpers.GetImageSource(ms, options.DecodeWidth);
                        }

                        if (options.ExtractCount > 0)
                            imgInfo.Flags = imgInfo.Flags | FileFlags.Archive_OpenSelf;

                        options.Callback?.Invoke(imgInfo);
                        extractCount++;
                        if (options.ExtractCount > 0 && extractCount >= options.ExtractCount)
                            break;
                    }
                }
                else
                {
                    //extract specified
                    foreach (var fileName in options.FileNames)
                    {
                        var imgInfo = new ImageInfo
                        {
                            FilePath = path,
                            Flags = FileFlags.Archive | FileFlags.Image,
                            FileName = fileName
                        };
                        using (var ms = new MemoryStream())
                        {
                            ext.ExtractFile(fileName, ms);
                            imgInfo.ImageSource = Helpers.GetImageSource(ms, options.DecodeWidth);
                        }

                        options.Callback?.Invoke(imgInfo);
                    }
                }

                //save password for the future
                Setting.MappedPasswords[path] = ext.Password;
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


        private void TN1_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            var tn = (Thumbnail)sender;
            var imgInfo = tn.ImageInfo;
            switch (e.ChangedButton) {
                case MouseButton.Left:
                    if (imgInfo.Flags.HasFlag(FileFlags.Directory)) {
                        ImageList.Clear();
                        Task.Run(() => LoadFolder(new DirectoryInfo(imgInfo.FilePath).Parent));
                    }
                    else if (imgInfo.Flags.HasFlag(FileFlags.Archive_OpenSelf)) {
                        ImageList.Clear();
                        Task.Run(() => LoadFile(imgInfo.FilePath, imgInfo.Flags,
                            new LoadOptions(Setting.ThumbnailSize.Width, callback: Callback_AddToImageList)));
                    }
                    else if (imgInfo.Flags.HasFlag(FileFlags.Image)) {
                        Task.Run(() => LoadFile(imgInfo.FilePath, imgInfo.Flags,
                            new LoadOptions(
                                fileNames: new[] { imgInfo.FileName },
                                callback: ii => Dispatcher.Invoke(() => new ViewWindow { ImageInfo = ii }.Show()))));
                    }
                    //
                    //            LoadFile(imgInfo.FilePath, ii => new ViewWindow {ImageInfo = ii}.Show(),
                    //                new LoadOptions(fileNames: new[] {imgInfo.FileName}));
                    break;
                case MouseButton.Right:
                    Task.Run(() => LoadPath(Directory.GetParent(imgInfo.FilePath).Parent));
                    break;
            }
            
        }
    }
}
