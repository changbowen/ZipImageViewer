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
using SizeInt = System.Drawing.Size;

namespace ZipImageViewer
{
    public partial class MainWindow : BorderlessWindow, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableKeyedCollection<string, ObjectInfo> ObjectList { get; } =
            new ObservableKeyedCollection<string, ObjectInfo>(o => o.VirtualPath);


        private DpiScale DpiScale;
        public double ThumbRealWidth => Setting.ThumbnailSize.Item1 / DpiScale.DpiScaleX;
        public double ThumbRealHeight => Setting.ThumbnailSize.Item2 / DpiScale.DpiScaleY;


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

        private CancellationTokenSource tknSrc_LoadThumb;
        private readonly object lock_LoadThumb = new object();

        public string InitialPath;

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

            if (InitialPath?.Length > 0)
                Task.Run(() => LoadPath(InitialPath));
            else if (Setting.LastPath?.Length > 0)
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
                            if (viewWin == null) new ViewWindow(this, ObjectList) { ObjectInfo = oi }.Show();
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
                        if (viewWin == null) new ViewWindow(this, ObjectList) { ObjectInfo = oi }.Show();
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

                        sources.Add(Helpers.GetImageSource(thumb.FullName, (SizeInt)Setting.ThumbnailSize));
                    }
                    var objInfo = new ObjectInfo(childDirInfo.FullName, FileFlags.Directory, sources.ToArray());
                    Callback_AddToImageList(objInfo);
                }
                //when the child is a file
                else {
                    var options = new LoadOptions(childInfo.FullName) {
                        Flags = Helpers.GetPathType(childInfo),
                        DecodeSize = (SizeInt)Setting.ThumbnailSize,
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
                DecodeSize = isThumb ? (SizeInt)Setting.ThumbnailSize : default,
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
                //some files may get loaded from cache therefore unaware of whether password is correct
                //the HashSet records processed files through retries
                var done = new HashSet<int>();

                for (int caseIdx = 0; caseIdx < 4; caseIdx++) {
                    if (tknSrc?.IsCancellationRequested == true) break;

                    var success = false;
                    switch (caseIdx) {
                        //first check if there is a match in saved passwords
                        case 0 when Setting.MappedPasswords.TryGetValue(options.FilePath, out ObservablePair<string, string> pwd):
                            options.Password = pwd.Item2;
                            success = ExtractZip(options, objInfo, done, tknSrc);
                            break;
                        //then try no password
                        case 1:
                            options.Password = null;
                            success = ExtractZip(options, objInfo, done, tknSrc);
                            break;
                        //then try all saved passwords with no filename
                        case 2:
                            foreach (var fp in Setting.FallbackPasswords) {
                                options.Password = fp;
                                success = ExtractZip(options, objInfo, done, tknSrc);
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
        private static bool ExtractZip(LoadOptions options, ObjectInfo objInfo, HashSet<int> done, CancellationTokenSource tknSrc = null)
        {
            if (tknSrc?.IsCancellationRequested == true) return false;
            SevenZipExtractor ext = null;
            try
            {
                ext = options.Password?.Length > 0 ? new SevenZipExtractor(options.FilePath, options.Password) :
                                                     new SevenZipExtractor(options.FilePath);
                var sources = new List<ImageSource>();
                var isThumb = options.DecodeSize.Width + options.DecodeSize.Height > 0;
                bool fromDisk = false;

                //get files in archive to extract
                int[] toDo;
                if (options.FileNames?.Length > 0) {
                    if (options.FileNames.Length == 1)
                        toDo = new[] { ext.ArchiveFileData.First(d => d.FileName == options.FileNames[0]).Index };
                    else
                        toDo = ext.ArchiveFileData
                            .Where(d => options.FileNames.Contains(d.FileName))
                            .Select(d => d.Index).ToArray();
                }
                else
                    toDo = ext.ArchiveFileData
                        .Where(d => !d.IsDirectory && Helpers.GetPathType(d.FileName) == FileFlags.Image)
                        .Select(d => d.Index).ToArray();

                foreach (var i in toDo) {
                    if (tknSrc?.IsCancellationRequested == true) return false;

                    //skip if already done
                    if (done.Contains(i)) continue;

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
                        fromDisk = true;
                        //load from disk
                        using (var ms = new MemoryStream()) {
                            ext.ExtractFile(i, ms);
                            source = Helpers.GetImageSource(ms, options.DecodeSize);
                        }
                        if (isThumb && source != null) SQLiteHelper.AddToThumbDB(source, thumbPath, options.DecodeSize);
                    }
                    sources.Add(source);
                    done.Add(i);

                    if (tknSrc?.IsCancellationRequested == true) return false;
                    if (options.CldInfoCallback != null) {
                        var cldInfo = new ObjectInfo(options.FilePath, FileFlags.Image | FileFlags.Archive, new[] { source }) { FileName = fileName };
                        options.CldInfoCallback.Invoke(cldInfo);
                    }

                    if (options.ExtractCount > 0 && done.Count >= options.ExtractCount)
                        break;
                }

                //update objInfo
                objInfo.ImageSources = sources.ToArray();

                //save password for the future
                if (fromDisk && options.Password?.Length > 0)
                    Setting.MappedPasswords[options.FilePath] = new ObservablePair<string, string>(options.FilePath, options.Password);
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
            var ctm = (ContextMenu)mi.CommandParameter;
            switch (mi.DataContext) {
                case ObjectInfo oi:
                    switch (mi.Header) {
                        case "View in Explorer":
                            Helpers.Run("explorer", $"/select, \"{oi.FileSystemPath}\"");
                            break;
                        case "Open in New Window":
                            if (oi.Flags.HasFlag(FileFlags.Directory) ||
                                oi.Flags.HasFlag(FileFlags.Archive)) {
                                var win = new MainWindow {
                                    InitialPath = oi.FileSystemPath
                                };
                                win.Show();
                            }
                            break;
                    }
                    break;
                //case ObservablePair<string, string> op:
                //    Helpers.Run(op.Item1, Helpers.CustomCmdArgsReplace(op.Item2, tn.ObjectInfo));
                //    break;
            }
            
        }

        private void CTM_Opened(object sender, RoutedEventArgs e) {
            //var ctm = (ContextMenu)sender;
            //var tn = (Thumbnail)ctm.PlacementTarget;
            //ctm.Tag = tn.ObjectInfo.FileSystemPath;

            //foreach (MenuItem item in mi.Items) {
            //    var op = (ObservablePair<string, string>)item.DataContext;
            //    item.DataContext = new ObservablePair<string, string>(op.Item1, op.Item2.Replace(@"%FileSystemPath%", tn.ObjectInfo.FileSystemPath));
            //}
        }
    }
}
