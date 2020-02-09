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
using System.Threading;
using System.Windows.Media;
using System.Linq;
using System.Windows.Data;

namespace ZipImageViewer
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableKeyedCollection<string, ObjectInfo> ObjectList { get; } =
            new ObservableKeyedCollection<string, ObjectInfo>(o => o.VirtualPath);


        private DpiScale DpiScale;
        public double ThumbRealWidth => Setting.ThumbnailSize.Width / DpiScale.DpiScaleX;
        public double ThumbRealHeight => Setting.ThumbnailSize.Height / DpiScale.DpiScaleY;


        private string currentPath = "";
        public string CurrentPath {
            get { return currentPath; }
            set {
                if (currentPath == value) return;
                currentPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPath)));
            }
        }

        public int ThumbChangeDelay => ObjectList.Count(oi => oi.ImageSources?.Length > 1) * 200 + App.Random.Next(2, 5) * 1000;

        private static CancellationTokenSource tknSrc_LoadThumb;
        private static readonly object lock_LoadThumb = new object();


        public MainWindow()
        {
            InitializeComponent();
        }

        #region MainWindow Event Handlers

        private void MainWin_Loaded(object sender, RoutedEventArgs e)
        {
            DpiScale = VisualTreeHelper.GetDpi(this);
            Setting.StaticPropertyChanged += Setting_StaticPropertyChanged;

            var view = (ListCollectionView)((CollectionViewSource)FindResource("ObjectListViewSource")).View;
            view.CustomSort = new Helpers.FolderSorter();

            ObjectList.CollectionChanged += (o1, e1) => {
                if (e1.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                    SV1.ScrollToTop();
            };

            if (Setting.LastPath?.Length > 0)
                Task.Run(() => LoadPath(Setting.LastPath));
            else if (Helpers.OpenFolderDialog(this) is string path)
                Task.Run(() => LoadPath(path));
        }

        private void Setting_StaticPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(Setting.ThumbnailSize) ||
                PropertyChanged == null) return;
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbRealWidth)));
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbRealHeight)));
        }

        private void MainWin_Unloaded(object sender, RoutedEventArgs e) {
            Setting.StaticPropertyChanged -= Setting_StaticPropertyChanged;

            tknSrc_LoadThumb?.Cancel();
            ObjectList.Clear();

            Setting.LastPath = CurrentPath;
            if (WindowState != WindowState.Maximized) {
                Setting.LastWindowSize = new Size(Width, Height);
            }

            if (Application.Current.Windows.Count == 0)
                Application.Current.Shutdown();
        }

        private void MainWin_DpiChanged(object sender, DpiChangedEventArgs e) {
            DpiScale = VisualTreeHelper.GetDpi(this);

            if (PropertyChanged == null) return;
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbRealWidth)));
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbRealHeight)));
        }

        private void MainWin_Drop(object sender, DragEventArgs e)
        {
            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (paths == null || paths.Length == 0) return;
            
            Task.Run(() => LoadPath(paths[0]));
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

        #endregion

        private void Callback_AddToImageList(ObjectInfo objInfo) {
            //ObjectList.Add(new ObjectInfo(objInfo.FileSystemPath, FileFlags.Unknown));
            ObjectList.Add(objInfo);
        }


        /// <summary>
        /// Display file system objects in the path as thumbnails, or open viewer depending on the file type and parameters.
        /// Main open logic is set here.
        /// Support cancellation. Used in Task.
        /// Flags are inferred from path. Not for opening image in an archive.
        /// </summary>
        internal void LoadPath(string path, ViewWindow viewWin = null) {
            LoadPath(new ObjectInfo(path), viewWin);
        }

        /// <summary>
        /// Display file system objects in the path as thumbnails, or open viewer depending on the file type and parameters.
        /// Main open logic is set here.
        /// Support cancellation. Used in Task.
        /// </summary>
        internal void LoadPath(ObjectInfo objInfo, ViewWindow viewWin = null) {
            //infer path type (flags)
            if (objInfo.Flags == FileFlags.Unknown)
                objInfo.Flags = Helpers.GetPathType(new DirectoryInfo(objInfo.FileSystemPath));

            // action based on flags
            if (objInfo.Flags.HasFlag(FileFlags.Directory)) {
                //directory -> load thumbs
                try {
                    tknSrc_LoadThumb?.Cancel();
                    Monitor.Enter(lock_LoadThumb);
                    tknSrc_LoadThumb = new CancellationTokenSource();
                    CurrentPath = objInfo.FileSystemPath;
                    ObjectList.Clear();
                    LoadFolder(new DirectoryInfo(objInfo.FileSystemPath), tknSrc: tknSrc_LoadThumb);
                }
                finally {
                    tknSrc_LoadThumb = null;
                    Monitor.Exit(lock_LoadThumb);
                }
            }
            else if (objInfo.Flags.HasFlag(FileFlags.Archive)) {
                if (objInfo.Flags.HasFlag(FileFlags.Image)) {
                    //image inside archive -> open viewer
                    LoadFile(objInfo.FileSystemPath, objInfo.Flags,
                        isThumb: false,
                        fileNames: new[] { objInfo.FileName },
                        cldInfoCb: oi => Dispatcher.Invoke(() => {
                            if (viewWin == null) new ViewWindow(this) { ObjectInfo = oi }.Show();
                            else viewWin.ObjectInfo = oi;
                        }));
                }
                else {
                    //archive itself -> extract and load thumbs
                    try {
                        tknSrc_LoadThumb?.Cancel();
                        Monitor.Enter(lock_LoadThumb);
                        tknSrc_LoadThumb = new CancellationTokenSource();
                        CurrentPath = objInfo.FileSystemPath;
                        ObjectList.Clear();
                        LoadFile(objInfo.FileSystemPath, objInfo.Flags, cldInfoCb: Callback_AddToImageList, tknSrc: tknSrc_LoadThumb);
                    }
                    finally {
                        tknSrc_LoadThumb = null;
                        Monitor.Exit(lock_LoadThumb);
                    }
                }
            }
            else if (objInfo.Flags.HasFlag(FileFlags.Image)) {
                //plain image file -> open viewer
                LoadFile(objInfo.FileSystemPath, objInfo.Flags,
                    isThumb: false,
                    objInfoCb: oi => Dispatcher.Invoke(() => {
                        if (viewWin == null) new ViewWindow(this) { ObjectInfo = oi }.Show();
                        else viewWin.ObjectInfo = oi;
                    }));
            }
        }

        /// <summary>
        /// Load thumbnails in folder.
        /// </summary>
        private void LoadFolder(DirectoryInfo dirInfo, CancellationTokenSource tknSrc = null) {
            CurrentPath = dirInfo.FullName;
            foreach (var childInfo in dirInfo.EnumerateFileSystemInfos()) {
                if (tknSrc?.IsCancellationRequested == true) return;

                //when the child is a folder
                if (childInfo is DirectoryInfo childDirInfo) {
                    //get the first few images
                    var thumbs = new List<FileSystemInfo>();
                    foreach (var fsInfo in childDirInfo.EnumerateFileSystemInfos()) {
                        if (tknSrc?.IsCancellationRequested == true) return;

                        var fType = Helpers.GetPathType(fsInfo);
                        if (fType != FileFlags.Image) continue;
                        thumbs.Add(fsInfo);
                        if (thumbs.Count == App.PreviewCount) break; //note that count can be 2 or less
                    }
                    //add to list
                    var sources = new List<ImageSource>();
                    foreach (var thumb in thumbs) {
                        if (tknSrc?.IsCancellationRequested == true) return;

                        sources.Add(Helpers.GetImageSource(thumb.FullName, Setting.ThumbnailSize));
                    }
                    var objInfo = new ObjectInfo(childDirInfo.FullName, FileFlags.Directory, sources.ToArray());
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
                    LoadFile(options, tknSrc);
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
            bool isThumb = true, string[] fileNames = null,
            Action<ObjectInfo> objInfoCb = null, Action<ObjectInfo> cldInfoCb = null,
            CancellationTokenSource tknSrc = null)
        {
            var options = new LoadOptions(filePath) {
                Flags = flags,
                DecodeSize = isThumb ? Setting.ThumbnailSize : default,
                FileNames = fileNames,
                ObjInfoCallback = objInfoCb,
                CldInfoCallback = cldInfoCb,
            };
            LoadFile(options, tknSrc);
        }

        private void LoadFile(LoadOptions options, CancellationTokenSource tknSrc = null)
        {
            if (tknSrc?.IsCancellationRequested == true) return;

            //objInfo to be returned
            var objInfo = new ObjectInfo(options.FilePath, options.Flags) {
                FileName = Path.GetFileName(options.FilePath)
            };

            //when file is an image
            if (options.Flags.HasFlag(FileFlags.Image) && !options.Flags.HasFlag(FileFlags.Archive))
                objInfo.ImageSources = new[] { Helpers.GetImageSource(options.FilePath, options.DecodeSize) };
            //when file is an archive
            else if (options.Flags.HasFlag(FileFlags.Archive)) {
                for (int caseIdx = 0; caseIdx < 4; caseIdx++) {
                    if (tknSrc?.IsCancellationRequested == true) break;

                    var success = false;
                    switch (caseIdx) {
                        //first check if there is a match in saved passwords
                        case 0 when Setting.MappedPasswords.TryGetValue(options.FilePath, out string pwd):
                            options.Password = pwd;
                            success = ExtractZip(options, objInfo, tknSrc);
                            break;
                        //then try no password
                        case 1:
                            options.Password = null;
                            success = ExtractZip(options, objInfo, tknSrc);
                            break;
                        //then try all saved passwords with no filename
                        case 2:
                            foreach (var fp in Setting.FallbackPasswords) {
                                options.Password = fp;
                                success = ExtractZip(options, objInfo, tknSrc);
                                if (success) break;
                            }
                            break;
                        case 3:
                            //if all fails mark with icon and when clicked
                            //prompt for a generic or dedicated password then extract with it
                            //-------------logic needed here-------------
                            Dispatcher.Invoke(() => objInfo.ImageSources = new[] { App.fa_exclamation });
                            objInfo.Comments = $"Extraction failed. Bad password or not supported image formats.";
                            break;
                    }

                    if (success) break;
                }
            }

            if (tknSrc?.IsCancellationRequested == true) return;
            options.ObjInfoCallback?.Invoke(objInfo);
        }

        //private static bool ExtractZip(LoadOptions options, ObjectInfo objInfo, CancellationTokenSource tknSrc = null)
        //{
        //    if (tknSrc?.IsCancellationRequested == true) return false;
        //    using (var fs = new FileStream(options.FilePath, FileMode.Open, FileAccess.Read)) {
        //        return ExtractZip(fs, options, objInfo, tknSrc);
        //    }
        //}

        /// <summary>
        /// Returns true or false based on whether extraction succeeds.
        /// </summary>
        private static bool ExtractZip(LoadOptions options, ObjectInfo objInfo, CancellationTokenSource tknSrc = null)
        {
            if (tknSrc?.IsCancellationRequested == true) return false;
            SevenZipExtractor ext = null;
            try
            {
                ext = options.Password == null ? new SevenZipExtractor(options.FilePath) :
                                                 new SevenZipExtractor(options.FilePath, options.Password);
                var sources = new List<ImageSource>();
                var isThumb = options.DecodeSize.Width + options.DecodeSize.Height > 0;

                //get files in archive to extract
                int[] toExtract;
                if (options.FileNames?.Length > 0) {
                    if (options.FileNames.Length == 1)
                        toExtract = new[] { ext.ArchiveFileData.First(d => d.FileName == options.FileNames[0]).Index };
                    else
                        toExtract = ext.ArchiveFileData
                            .Where(d => options.FileNames.Contains(d.FileName))
                            .Select(d => d.Index).ToArray();
                }
                else
                    toExtract = ext.ArchiveFileData
                        .Where(d => !d.IsDirectory && Helpers.GetPathType(d.FileName) == FileFlags.Image)
                        .Select(d => d.Index).ToArray();

                int extractCount = 0;
                foreach (var i in toExtract) {
                    if (tknSrc?.IsCancellationRequested == true) return false;

                    //check if file is supported
                    var fileName = ext.ArchiveFileData[i].FileName;
#if DEBUG
                    Console.WriteLine("Extracting " + fileName);
#endif
                    ImageSource source = null;
                    var thumbPath = Path.Combine(options.FilePath, fileName);
                    if (isThumb) {
                        //try load from cache
                        source = SQLiteHelper.GetFromThumbDB(thumbPath, options.DecodeSize);
                    }
                    if (source == null) {
                        //load from disk
                        using (var ms = new MemoryStream()) {
                            ext.ExtractFile(i, ms);
                            source = Helpers.GetImageSource(ms, options.DecodeSize);
                        }
                        if (isThumb && source != null) SQLiteHelper.AddToThumbDB(source, thumbPath, options.DecodeSize);
                    }
                    sources.Add(source);
                    extractCount++;

                    if (tknSrc?.IsCancellationRequested == true) return false;
                    if (options.CldInfoCallback != null) {
                        var cldInfo = new ObjectInfo(options.FilePath, FileFlags.Image | FileFlags.Archive, new[] { source }) { FileName = fileName };
                        options.CldInfoCallback.Invoke(cldInfo);
                    }

                    if (options.ExtractCount > 0 && extractCount >= options.ExtractCount)
                        break;
                }

                //update objInfo
                objInfo.ImageSources = sources.ToArray();

                //save password for the future
                if (options.Password != null)
                    Setting.MappedPasswords[options.FilePath] = options.Password;
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


        private void TN1_MouseUp(object sender, MouseButtonEventArgs e) {
            var tn = (Thumbnail)sender;
            var objInfo = tn.ObjectInfo;
            if (e.ClickCount == 1) {
                switch (e.ChangedButton) {
                    case MouseButton.Left:
                        Task.Run(() => LoadPath(objInfo));
                        break;

                }
            }
        }


        private void Nav_Up(object sender, RoutedEventArgs e) {
            Task.Run(() => LoadPath(Path.GetDirectoryName(CurrentPath)));
        }

        private void Show_Options(object sender, RoutedEventArgs e) {
            new SettingsWindow(this).ShowDialog();
        }

        private void CTM_Click(object sender, RoutedEventArgs e) {
            var mi = (MenuItem)sender;
            var tn = (Thumbnail)mi.CommandTarget;
            switch (mi.Header) {
                case "View in Explorer":
                    Helpers.Run("explorer", $"/select, \"{tn.ObjectInfo.FileSystemPath}\"");
                    break;
                default:
                    break;
            }
        }
    }
}
