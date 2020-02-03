using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SevenZip;
using Path = System.IO.Path;
using System.ComponentModel;

namespace ZipImageViewer
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public ObservableKeyedCollection<string, ObjectInfo> ObjectList { get; set; } =
            new ObservableKeyedCollection<string, ObjectInfo>(ii => ii.VirtualPath);
        public double ThumbWidth => PresentationSource.FromVisual(this).CompositionTarget.TransformFromDevice.M11 * Setting.ThumbnailSize.Width;
        public double ThumbHeight => PresentationSource.FromVisual(this).CompositionTarget.TransformFromDevice.M22 * Setting.ThumbnailSize.Height;

        private string currentPath;
        public string CurrentPath {
            get { return currentPath; }
            set {
                if (currentPath == value) return;
                currentPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPath)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWin_Loaded(object sender, RoutedEventArgs e)
        {
#if DEBUG
            //Topmost = true;
            Task.Run(() => LoadPath(@"E:\Pictures\new folder.sdf\To Doa"));
#else
            if (Helpers.OpenFolderDialog(this) is string path) Task.Run(() => LoadPath(path));
#endif

        }

        private void MainWin_Drop(object sender, DragEventArgs e)
        {
            ObjectList.Clear();
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths == null || paths.Length == 0) return;

            Task.Run(() => {
                LoadPath(paths[0]);
            });
        }

        private void Callback_AddToImageList(ObjectInfo objInfo) {
            ObjectList.Add(objInfo);
        }

        /// <summary>
        /// Flags are inferred from path. Not for opening image in an archive.
        /// </summary>
        internal void LoadPath(string path, ViewWindow viewWin = null) {
            LoadPath(new ObjectInfo(path, FileFlags.Unknown), viewWin);
        }

        /// <summary>
        /// Display file system objects in the path as thumbnails, or open viewer depending on the file type and parameters.
        /// Main open logic is set here.
        /// </summary>
        internal void LoadPath(ObjectInfo objInfo, ViewWindow viewWin = null) {
            //infer path type (flags)
            if (objInfo.Flags == FileFlags.Unknown)
                objInfo.Flags = Helpers.GetPathType(new DirectoryInfo(objInfo.FileSystemPath));
            
            // action based on flags
            if (objInfo.Flags.HasFlag(FileFlags.Directory)) {
                //directory -> load thumbs
                CurrentPath = objInfo.FileSystemPath;
                ObjectList.Clear();
                LoadFolder(new DirectoryInfo(objInfo.FileSystemPath));
            }
            else if (objInfo.Flags.HasFlag(FileFlags.Archive)) {
                if (objInfo.Flags.HasFlag(FileFlags.Image)) {
                    //image inside archive -> open viewer
                    LoadFile(objInfo.FileSystemPath, objInfo.Flags,
                        isThumb: false,
                        fileNames: new[] { objInfo.FileName },
                        cldInfoCb: oi => Dispatcher.Invoke(() => {
                            if (viewWin == null) new ViewWindow() { ObjectInfo = oi }.Show();
                            else viewWin.ObjectInfo = oi;
                        }));
                }
                else {
                    //archive itself -> extract and load thumbs
                    CurrentPath = objInfo.FileSystemPath;
                    ObjectList.Clear();
                    LoadFile(objInfo.FileSystemPath, objInfo.Flags, cldInfoCb: Callback_AddToImageList);
                }
                
            }
            else if (objInfo.Flags.HasFlag(FileFlags.Image)) {
                //plain image file -> open viewer
                LoadFile(objInfo.FileSystemPath, objInfo.Flags,
                    isThumb: false,
                    objInfoCb: oi => Dispatcher.Invoke(() => {
                        if (viewWin == null) new ViewWindow() { ObjectInfo = oi }.Show();
                        else viewWin.ObjectInfo = oi;
                    }));
            }
        }

        /// <summary>
        /// Load thumbnails in folder.
        /// </summary>
        private void LoadFolder(DirectoryInfo dirInfo) {
            CurrentPath = dirInfo.FullName;
            foreach (var childInfo in dirInfo.EnumerateFileSystemInfos()) {
                //when the child is a folder
                if (childInfo is DirectoryInfo childDirInfo) {
                    //get the first few images
                    var thumbs = new List<FileSystemInfo>();
                    foreach (var fsInfo in childDirInfo.EnumerateFileSystemInfos()) {
                        var fType = Helpers.GetPathType(fsInfo);
                        if (fType != FileFlags.Image) continue;
                        thumbs.Add(fsInfo);
                        if (thumbs.Count == App.PreviewCount) break; //note that count can be 2 or less
                    }
                    //add to list
                    var objInfo = new ObjectInfo(childDirInfo.FullName, FileFlags.Directory);
                    foreach (var thumb in thumbs) {
                        objInfo.ImageSources.Add(Helpers.GetImageSource(thumb.FullName, Setting.ThumbnailSize));
                    }
                    Callback_AddToImageList(objInfo);
                }
                //when the child is a file
                else {
                    var options = new LoadOptions(childInfo.FullName) {
                        Flags = Helpers.GetPathType(childInfo),
                        DecodeSize = Setting.ThumbnailSize,
                        ObjInfoCallback = Callback_AddToImageList,
                    };
                    if (options.Flags == FileFlags.Archive) options.ExtractCount = App.PreviewCount;
                    LoadFile(options);
                }
                
            }
        }

        /// <summary>
        /// Load image based on the type of file and try passwords when possible.
        /// If filePath points to an archive, LoadOptions.ExtractCount will be used to decide how many images in the archive are processed.
        /// Can be called from a background thread.
        /// Callback can be used to manipulate the loaded images. For e.g. display it in the ViewWindow, or add to ObjectList as thumbnails.
        /// Callback is called for each image loaded.
        /// Use Dispatcher if callback needs to access the UI thread.
        /// <param name="flags">Only checks for Image and Archive.</param>
        /// </summary>
        private void LoadFile(string filePath, FileFlags flags,
            bool isThumb = true, string[] fileNames = null, Action<ObjectInfo> objInfoCb = null, Action<ObjectInfo> cldInfoCb = null)
        {
            var options = new LoadOptions(filePath) {
                Flags = flags,
                DecodeSize = isThumb ? Setting.ThumbnailSize : default,
                FileNames = fileNames,
                ObjInfoCallback = objInfoCb,
                CldInfoCallback = cldInfoCb,
            };
            LoadFile(options);
        }

        private void LoadFile(LoadOptions options)
        {
            //objInfo to be returned
            var objInfo = new ObjectInfo(options.FilePath, options.Flags) {
                FileName = Path.GetFileName(options.FilePath)
            };

            //when file is an image
            if (options.Flags.HasFlag(FileFlags.Image) && !options.Flags.HasFlag(FileFlags.Archive))
                objInfo.ImageSources.Add(Helpers.GetImageSource(options.FilePath, options.DecodeSize));
            //when file is an archive
            else if (options.Flags.HasFlag(FileFlags.Archive)) {
                while (true)
                {
                    //first check if there is a match in saved passwords
                    var pwd = Setting.MappedPasswords[options.FilePath];
                    if (pwd != null)
                    {
                        //matching password found
                        options.Password = pwd;
                        if (ExtractZip(options, objInfo)) break;
                    }

                    //then try no password
                    options.Password = null;
                    if (ExtractZip(options, objInfo)) break;

                    //then try all saved passwords with no filename
                    foreach (var fp in Setting.FallbackPasswords) {
                        options.Password = fp;
                        if (ExtractZip(options, objInfo)) break;
                    }

                    //if all fails prompt for a generic or dedicated password
                    //then extract with it
                    //-------------logic needed here-------------


                    //exit loop
                    break;
                }
            }

            options.ObjInfoCallback?.Invoke(objInfo);
        }

        /// <summary>
        /// Returns true or false based on whether extraction succeeds.
        /// </summary>
        private static bool ExtractZip(LoadOptions options, ObjectInfo objInfo)
        {
            using (var fs = new FileStream(options.FilePath, FileMode.Open, FileAccess.Read))
            {
                return ExtractZip(fs, options, objInfo);
            }
        }

        /// <summary>
        /// Returns true or false based on whether extraction succeeds.
        /// </summary>
        private static bool ExtractZip(Stream stream, LoadOptions options, ObjectInfo objInfo)
        {
            SevenZipExtractor ext = null;
            stream.Position = 0;
            try
            {
                ext = new SevenZipExtractor(stream, options.Password);

                //extract all or by ExtractCount
                if (options.FileNames == null || options.FileNames.Length == 0) {
                    int extractCount = 0;
                    //if (options.ExtractCount > 0)
                    //    objInfo.Flags |= FileFlags.Archive_OpenSelf;

                    for (int i = 0; i < ext.ArchiveFileData.Count; i++) {
                        //check if file is supported
                        var imgfile = ext.ArchiveFileData[i];
                        if (imgfile.IsDirectory || Helpers.GetPathType(imgfile.FileName) != FileFlags.Image) continue;
#if DEBUG
                        Console.WriteLine("Extracting " + imgfile.FileName);
#endif
                        var cldInfo = new ObjectInfo(options.FilePath, FileFlags.Image | FileFlags.Archive) {
                            FileName = imgfile.FileName,
                        };
                        using (var ms = new MemoryStream())
                        {
                            ext.ExtractFile(i, ms);
                            var source = Helpers.GetImageSource(ms, options.DecodeSize);
                            cldInfo.ImageSources.Add(source);
                            objInfo.ImageSources.Add(source);
                        }
                        extractCount++;
                        options.CldInfoCallback?.Invoke(cldInfo);

                        if (options.ExtractCount > 0 && extractCount >= options.ExtractCount)
                            break;
                    }
                }
                //extract specified
                else {
                    foreach (var fileName in options.FileNames) {
#if DEBUG
                        Console.WriteLine("Extracting " + fileName);
#endif
                        var cldInfo = new ObjectInfo(options.FilePath, FileFlags.Image | FileFlags.Archive) {
                            FileName = fileName,
                        };
                        using (var ms = new MemoryStream())
                        {
                            ext.ExtractFile(fileName, ms);
                            var source = Helpers.GetImageSource(ms, options.DecodeSize);
                            cldInfo.ImageSources.Add(source);
                            objInfo.ImageSources.Add(source);
                        }
                        options.CldInfoCallback?.Invoke(cldInfo);
                    }
                }

                //save password for the future
                Setting.MappedPasswords[options.FilePath] = ext.Password;
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


        private void MainWin_MouseDown(object sender, MouseButtonEventArgs e) {
            if (!(e.OriginalSource is ScrollViewer)) return;
            if (e.ClickCount == 1 && e.ChangedButton == MouseButton.Right)
                Nav_Up(null, null);
            if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left) {
                if (Helpers.OpenFolderDialog(this) is string path) Task.Run(() => LoadPath(path));
            }
            e.Handled = true;
        }

        private void TN1_MouseUp(object sender, MouseButtonEventArgs e) {
            var tn = (Thumbnail)sender;
            var objInfo = tn.ObjectInfo;
            if (e.ClickCount == 1) {
                switch (e.ChangedButton) {
                    case MouseButton.Left:
                        Task.Run(() => LoadPath(objInfo));
                        break;
                        //case MouseButton.Right:
                        //    ObjectList.Clear();
                        //    Task.Run(() => LoadPath(objInfo.Parent));
                        //    break;
                }
            }
        }

        //private void CTM1_Clicked(object sender, RoutedEventArgs e) {
        //    switch (((MenuItem)sender).Header) {
        //        case "Options":
        //            new SettingsWindow().ShowDialog();
        //            break;
        //    }
        //}

        private void Nav_Up(object sender, RoutedEventArgs e) {
            Task.Run(() => LoadPath(Path.GetDirectoryName(CurrentPath)));
        }

        private void Show_Options(object sender, RoutedEventArgs e) {
            new SettingsWindow().ShowDialog();
        }
    }
}
