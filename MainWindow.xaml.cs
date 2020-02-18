using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using SevenZip;
using Path = System.IO.Path;
using System.ComponentModel;
using System.Threading;
using System.Windows.Media;
using System.Linq;
using System.Windows.Data;
using SizeInt = System.Drawing.Size;
using System.Windows.Threading;

namespace ZipImageViewer
{
    public partial class MainWindow : BorderlessWindow, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableKeyedCollection<string, ObjectInfo> ObjectList { get; } = new ObservableKeyedCollection<string, ObjectInfo>(o => o.VirtualPath);

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

        public int ThumbChangeDelay => Convert.ToInt32((virWrapPanel.VisualChildrenCount * 200 + App.Random.Next(2, 7) * 1000) * Setting.ThumbSwapDelayMultiplier);

        internal CancellationTokenSource tknSrc_LoadThumb;
        private readonly object lock_LoadThumb = new object();

        public string InitialPath;


        private VirtualizingWrapPanel virWrapPanel;
        private Rect lastWindowRect;
        internal Rect lastViewWindowRect;

        public MainWindow()
        {
            InitializeComponent();
        }


        #region MainWindow Event Handlers

        private void MainWin_Loaded(object sender, RoutedEventArgs e)
        {
            if (App.ContextMenuWin == null)
                App.ContextMenuWin = new ContextMenuWindow();

            virWrapPanel = Helpers.GetVisualChild<VirtualizingWrapPanel>(TV1);

            DpiScale = VisualTreeHelper.GetDpi(this);
            Setting.ThumbnailSize.PropertyChanged += ThumbnailSizeChanged;

            var view = (ListCollectionView)((CollectionViewSource)FindResource("ObjectListViewSource")).View;
            view.CustomSort = new FolderSorter();

            //load last path or open dialog
            if (InitialPath?.Length > 0)
                Task.Run(() => LoadPath(InitialPath));
            else if (Setting.LastPath?.Length > 0)
                Task.Run(() => LoadPath(Setting.LastPath));
            else
                openFolderPrompt();
        }

        private void ThumbnailSizeChanged(object sender, PropertyChangedEventArgs e) {
            if (PropertyChanged == null) return;
            if (e.PropertyName == nameof(Setting.ThumbnailSize.Item1))
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbRealWidth)));
            if (e.PropertyName == nameof(Setting.ThumbnailSize.Item2))
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbRealHeight)));
        }

        private async void MainWin_Closing(object sender, CancelEventArgs e) {
            Setting.ThumbnailSize.PropertyChanged -= ThumbnailSizeChanged;

            tknSrc_LoadThumb?.Cancel();
            while (tknSrc_LoadThumb != null) {
                await Task.Delay(100);
            }
            Dispatcher.Invoke(() => ObjectList.Clear());

            Setting.LastPath = CurrentPath;
            if (WindowState != WindowState.Maximized) {
                Setting.LastWindowSize = new Size(Width, Height);
            }

            if (Application.Current.Windows.Cast<Window>().Count(w => w is MainWindow) == 0)
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

        private void TV1_MouseDown(object sender, MouseButtonEventArgs e) {
            if (!e.Source.Equals(sender)) return;
            if (e.ClickCount == 1 && e.ChangedButton == MouseButton.Right) {
                Nav_Up(null, null);
                e.Handled = true;
            }
            else if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left) {
                openFolderPrompt();
                e.Handled = true;
            }
        }

        #endregion


        #region Private Helper Methods

        private void openFolderPrompt() {
            Helpers.OpenFolderDialog(this, path => Task.Run(() => LoadPath(path)));
        }

        private void Callback_AddToImageList(ObjectInfo objInfo) {
            //var add = true;
            //Helpers.UpdateSourcePaths(objInfo);
            //Dispatcher.Invoke(() => {
            //    if (AuxVisibility == Visibility.Collapsed && (objInfo.SourcePaths == null || objInfo.SourcePaths.Length == 0))
            //        add = false;
            //});
            //if (add) ObjectList.Add(objInfo);
            if (Setting.ImmersionMode && objInfo.SourcePaths == null) {
                Helpers.UpdateSourcePaths(objInfo);//update needed to exclude items that do not have thumbs
                if (objInfo.SourcePaths == null || objInfo.SourcePaths.Length == 0)
                    return;
            }
            ObjectList.Add(objInfo);
        }

        private void clearObjectList() {
            foreach (var objInfo in ObjectList) {
                objInfo.SourcePaths = null;
                objInfo.ImageSource = null;
            }
            ObjectList.Clear();
            Dispatcher.Invoke(() => virWrapPanel.ScrollOwner.ScrollToTop());
        }

        #endregion


        #region Load Methods
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
                    clearObjectList();
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
                    LoadFile(new LoadOptions(objInfo.FileSystemPath) {
                        Flags = objInfo.Flags,
                        LoadImage = true,
                        FileNames = new[] { objInfo.FileName },
                        CldInfoCallback = oi => Dispatcher.Invoke(() => {
                            if (viewWin == null) new ViewWindow(this, ObjectList) { ObjectInfo = oi }.Show();
                            else viewWin.ObjectInfo = oi;
                        }),
                    });
                }
                else {
                    //archive itself -> extract and load thumbs
                    try {
                        tknSrc_LoadThumb?.Cancel();
                        Monitor.Enter(lock_LoadThumb);
                        tknSrc_LoadThumb = new CancellationTokenSource();
                        CurrentPath = objInfo.FileSystemPath;
                        clearObjectList();
                        LoadFile(new LoadOptions(objInfo.FileSystemPath) {
                            Flags = objInfo.Flags,
                            LoadImage = true,
                            DecodeSize = (SizeInt)Setting.ThumbnailSize,
                            CldInfoCallback = Callback_AddToImageList,
                        }, tknSrc_LoadThumb);
                    }
                    finally {
                        tknSrc_LoadThumb = null;
                        Monitor.Exit(lock_LoadThumb);
                    }
                }
            }
            else if (objInfo.Flags.HasFlag(FileFlags.Image)) {
                //plain image file -> open viewer
                LoadFile(new LoadOptions(objInfo.FileSystemPath) {
                    Flags = objInfo.Flags,
                    LoadImage = true,
                    ObjInfoCallback = oi => Dispatcher.Invoke(() => {
                        if (viewWin == null) new ViewWindow(this, ObjectList) { ObjectInfo = oi }.Show();
                        else viewWin.ObjectInfo = oi;
                    }),
                });
            }
        }

        /// <summary>
        /// Load thumbnails in folder.
        /// </summary>
        private void LoadFolder(DirectoryInfo dirInfo, CancellationTokenSource tknSrc = null) {
            CurrentPath = dirInfo.FullName;
            foreach (var childInfo in dirInfo.EnumerateFileSystemInfos()) {
                if (tknSrc?.IsCancellationRequested == true) return;

                var flag = Helpers.GetPathType(childInfo);
                Callback_AddToImageList(new ObjectInfo(childInfo.FullName, flag) {
                    FileName = childInfo.Name,
                });

                //throttle the load
                if (flag != FileFlags.Unknown) Thread.Sleep(50);
                //how to sleep only when childinfo thumbnail is loading images?
            }
        }


        /// <summary>
        /// Load image based on the type of file and try passwords when possible.
        /// <para>
        /// If filePath points to an archive, ObjectInfo.Flags in ObjInfoCallback will contain FileFlag.Error when extraction fails.
        /// ObjectInfo.SourcePaths in ObjInfoCallback contains the file list inside archive.
        /// ObjectInfo in CldInfoCallback contains information for files inside archive.
        /// </para>
        /// Should be called from a background thread.
        /// Callback can be used to manipulate the loaded images. For e.g. display it in the ViewWindow, or add to ObjectList as thumbnails.
        /// Callback is called for each image loaded.
        /// Use Dispatcher if callback needs to access the UI thread.
        /// <param name="flags">Only checks for Image and Archive.</param>
        /// </summary>
        internal static void LoadFile(LoadOptions options, CancellationTokenSource tknSrc = null)
        {
            if (tknSrc?.IsCancellationRequested == true) return;

            //objInfo to be returned
            var objInfo = new ObjectInfo(options.FilePath, options.Flags) {
                FileName = Path.GetFileName(options.FilePath)
            };

            //when file is an image
            if (options.Flags.HasFlag(FileFlags.Image) && !options.Flags.HasFlag(FileFlags.Archive)) {
                objInfo.SourcePaths = new[] { options.FilePath };
                if (options.LoadImage)
                    objInfo.ImageSource = Helpers.GetImageSource(options.FilePath, options.DecodeSize);
            }
            //when file is an archive
            else if (options.Flags.HasFlag(FileFlags.Archive)) {
                //some files may get loaded from cache therefore unaware of whether password is correct
                //the HashSet records processed files through retries
                var done = new HashSet<string>();
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
                            //if all fails, prompt for password then extract with it
                            if (options.LoadImage &&
                                (options.FileNames == null || options.DecodeSize == default)) {
                                //ask for password when opening explicitly the archive or opening viewer for images inside archive
                                while (!success) {
                                    string pwd = null;
                                    bool isFb = true;
                                    Application.Current.Dispatcher.Invoke(() => {
                                        var win = new InputWindow();
                                        if (win.ShowDialog() == true) {
                                            pwd = win.TB_Password.Text;
                                            isFb = win.CB_Fallback.IsChecked == true;
                                        }
                                        win.Close();
                                    });

                                    if (!string.IsNullOrEmpty(pwd)) {
                                        options.Password = pwd;
                                        success = ExtractZip(options, objInfo, done, tknSrc);
                                        if (success) {
                                            //make sure the password is saved when task is cancelled
                                            Setting.MappedPasswords.Remove(options.FilePath);
                                            Setting.MappedPasswords.Add(new ObservablePair<string, string>(options.FilePath, pwd));
                                            if (isFb) {
                                                Setting.FallbackPasswords.Remove(pwd);
                                                Setting.FallbackPasswords.Add(new Observable<string>(pwd));
                                            }
                                            break;
                                        }
                                    }
                                    else break;
                                }
                            }
                            if (!success) {
                                objInfo.Flags |= FileFlags.Error;
                                objInfo.Comments = $"Extraction failed. Bad password or not supported image formats.";
                            }
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
        private static bool ExtractZip(LoadOptions options, ObjectInfo objInfo, HashSet<string> done, CancellationTokenSource tknSrc = null)
        {
            var success = false;
            if (tknSrc?.IsCancellationRequested == true) return success;
            SevenZipExtractor ext = null;
            try
            {
                ext = options.Password?.Length > 0 ? new SevenZipExtractor(options.FilePath, options.Password) :
                                                     new SevenZipExtractor(options.FilePath);
                var isThumb = options.DecodeSize.Width + options.DecodeSize.Height > 0;
                bool fromDisk = false;

                //get files in archive to extract
                string[] toDo;
                if (options.FileNames?.Length > 0)
                    toDo = options.FileNames;
                else
                    toDo = ext.ArchiveFileData
                        .Where(d => !d.IsDirectory && Helpers.GetPathType(d.FileName) == FileFlags.Image)
                        .Select(d => d.FileName).ToArray();

                foreach (var fileName in toDo) {
                    if (tknSrc?.IsCancellationRequested == true) break;

                    //skip if already done
                    if (done.Contains(fileName)) continue;

                    ImageSource source = null;
                    if (options.LoadImage) {
                        var thumbPathInDb = Path.Combine(options.FilePath, fileName);
                        if (isThumb) {
                            //try load from cache
                            source = SQLiteHelper.GetFromThumbDB(thumbPathInDb, options.DecodeSize);
                        }
                        if (source == null) {
#if DEBUG
                            Console.WriteLine("Extracting " + fileName);
#endif
                            fromDisk = true;
                            //load from disk
                            using (var ms = new MemoryStream()) {
                                ext.ExtractFile(fileName, ms);
                                success = true; //if the task is cancelled, success info is still returned correctly.
                                source = Helpers.GetImageSource(ms, options.DecodeSize);
                            }
                            if (isThumb && source != null) SQLiteHelper.AddToThumbDB(source, thumbPathInDb, options.DecodeSize);
                        }
                    }

                    if (options.CldInfoCallback != null) {
                        var cldInfo = new ObjectInfo(options.FilePath, FileFlags.Image | FileFlags.Archive) {
                            FileName = fileName,
                            SourcePaths = new[] { fileName },
                            ImageSource = source,
                        };
                        options.CldInfoCallback.Invoke(cldInfo);
                    }
                    
                    done.Add(fileName);
                }

                //update objInfo
                objInfo.SourcePaths = toDo;

                //save password for the future
                if (fromDisk && options.Password?.Length > 0 &&
                    (!Setting.MappedPasswords.Contains(options.FilePath) ||
                      Setting.MappedPasswords[options.FilePath].Item2 != options.Password)) {
                    Setting.MappedPasswords.Remove(options.FilePath);
                    Setting.MappedPasswords.Add(new ObservablePair<string, string>(options.FilePath, options.Password));
                }

                return true; //it is considered successful if the code reaches here
            }
            catch (Exception ex)
            {
                if (ex is ExtractionFailedException ||
                    ex is SevenZipArchiveException ||
                    ex is NotSupportedException) return false;
                
                if (ext != null) {
                    ext.Dispose();
                    ext = null;
                }
                throw;
            }
            finally
            {
                if (ext != null) {
                    ext.Dispose();
                    ext = null;
                }
            }
        }

        #endregion


        #region Misc Event Handlers

        private void TN1_Click(object sender, MouseButtonEventArgs e) {
            if (e.Source.Equals(sender)) {
                e.Handled = true;
                var tn = (Thumbnail)sender;
                var objInfo = tn.ObjectInfo;
                if (e.ClickCount != 1) return;
                switch (e.ChangedButton) {
                    case MouseButton.Left:
                        Task.Run(() => LoadPath(objInfo));
                        break;
                    case MouseButton.Right:
                        App.ContextMenuWin.Owner = this;
                        App.ContextMenuWin.ObjectInfo = objInfo;
                        App.ContextMenuWin.FadeIn();
                        break;
                }
            }
        }


        private void Nav_Up(object sender, RoutedEventArgs e) {
            Task.Run(() => LoadPath(Path.GetDirectoryName(CurrentPath)));
        }

        private void Sidebar_Click(object sender, RoutedEventArgs e) {
            switch (((FrameworkContentElement)sender).Name) {
                case nameof(HY_Open):
                    openFolderPrompt();
                    break;
                case nameof(HY_Options):
                    var win = new SettingsWindow(this);
                    win.ShowDialog();
                    win.Close();
                    break;
                case nameof(HY_ImmersionMode):
                    Setting.ImmersionMode = !Setting.ImmersionMode;
                    if (Setting.ImmersionMode)
                    {
                        virWrapPanel.ScrollOwner.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                        if (WindowState == WindowState.Maximized) {
                            //go fullscreen if alreayd maximized
                            WindowState = WindowState.Normal;
                            lastWindowRect = new Rect(Left, Top, Width, Height);
                            var info = NativeHelpers.GetMonitorFromWindow(this);
                            Top = info.Top;
                            Left = info.Left;
                            Width = info.Width;
                            Height = info.Height;
                        }
                    }
                    else
                    {
                        virWrapPanel.ScrollOwner.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                        if (lastWindowRect.Size.Width + lastWindowRect.Size.Height > 0) {
                            Top = lastWindowRect.Top;
                            Left = lastWindowRect.Left;
                            Width = lastWindowRect.Width;
                            Height = lastWindowRect.Height;
                        }
                    }
                    break;
                case nameof(HY_Close):
                    Close();
                    break;
            }
        }

        protected override void OnStateChanged(EventArgs e) {
            //override state behavior to enable fullscreen in immersion mode
            if (WindowState == WindowState.Maximized && Setting.ImmersionMode) {
                WindowState = WindowState.Normal;
                var info = NativeHelpers.GetMonitorFromWindow(this);
                if (Top == info.Top && Left == info.Left && Width == info.Width && Height == info.Height) {
                    //restore
                    Top = lastWindowRect.Top;
                    Left = lastWindowRect.Left;
                    Width = lastWindowRect.Width;
                    Height = lastWindowRect.Height;
                }
                else {
                    //fullscreen
                    lastWindowRect = new Rect(Left, Top, Width, Height);
                    Top = info.Top;
                    Left = info.Left;
                    Width = info.Width;
                    Height = info.Height;
                }
            }
        }


        //private void CTM_Click(object sender, RoutedEventArgs e) {
        //    var mi = (MenuItem)sender;
        //    var ctm = (ContextMenu)mi.CommandParameter;
        //    switch (mi.DataContext) {
        //        case ObjectInfo oi:
        //            switch (mi.Header) {
        //                case "View in Explorer":
        //                    Helpers.Run("explorer", $"/select, \"{oi.FileSystemPath}\"");
        //                    break;
        //                case "Open in New Window":
        //                    if (oi.Flags.HasFlag(FileFlags.Image)) {
        //                        LoadPath(oi);
        //                    }
        //                    else if (oi.Flags.HasFlag(FileFlags.Directory) ||
        //                        oi.Flags.HasFlag(FileFlags.Archive)) {
        //                        var win = new MainWindow {
        //                            InitialPath = oi.FileSystemPath
        //                        };
        //                        win.Show();
        //                    }
        //                    break;
        //            }
        //            break;
        //        //case ObservablePair<string, string> op:
        //        //    Helpers.Run(op.Item1, Helpers.CustomCmdArgsReplace(op.Item2, tn.ObjectInfo));
        //        //    break;
        //    }
            
        //}

        //private void CTM_Opened(object sender, RoutedEventArgs e) {
        //    //var ctm = (ContextMenu)sender;
        //    //var tn = (Thumbnail)ctm.PlacementTarget;
        //    //ctm.Tag = tn.ObjectInfo.FileSystemPath;

        //    //foreach (MenuItem item in mi.Items) {
        //    //    var op = (ObservablePair<string, string>)item.DataContext;
        //    //    item.DataContext = new ObservablePair<string, string>(op.Item1, op.Item2.Replace(@"%FileSystemPath%", tn.ObjectInfo.FileSystemPath));
        //    //}
        //}

        #endregion

    }
}
