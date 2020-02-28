using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using SevenZip;
using System.ComponentModel;
using System.Threading;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Threading;
using System.Data;
using Path = System.IO.Path;
using SizeInt = System.Drawing.Size;
using static ZipImageViewer.LoadHelper;

namespace ZipImageViewer
{
    public partial class MainWindow : BorderlessWindow, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableKeyedCollection<string, ObjectInfo> ObjectList { get; } = new ObservableKeyedCollection<string, ObjectInfo>(o => o.VirtualPath);

        private DpiScale DpiScale;
        public double ThumbRealWidth => Setting.ThumbnailSize.Item1 / DpiScale.DpiScaleX;
        public double ThumbRealHeight => Setting.ThumbnailSize.Item2 / DpiScale.DpiScaleY;

        public string InitialPath;

        private string currentPath = "";
        public string CurrentPath {
            get { return currentPath; }
            set {
                if (currentPath == value) return;
                currentPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPath)));
            }
        }

        public int ThumbChangeDelay => Convert.ToInt32(Setting.ThumbSwapDelayMultiplier * (
            virWrapPanel.Children.Cast<ContentPresenter>().Count(cp => ((ObjectInfo)cp.Content).SourcePaths?.Length > 0) * 200 + App.Random.Next(2, 7) * 1000));

        internal CancellationTokenSource tknSrc_LoadThumb;
        internal readonly object lock_LoadThumb = new object();

        private VirtualizingWrapPanel virWrapPanel;
        private Rect lastWindowRect;
        internal Rect lastViewWindowRect;

        public MainWindow()
        {
            InitializeComponent();
            Width = Setting.LastWindowSize.Width;
            Height = Setting.LastWindowSize.Height;
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

        private void MainWin_Closed(object sender, EventArgs e) {
            Helpers.ShutdownCheck();
        }

        private bool reallyClose = false;
        private async void MainWin_Closing(object sender, CancelEventArgs e) {
            if (reallyClose) return;

            //start cleaning up
            e.Cancel = true;
            Setting.ThumbnailSize.PropertyChanged -= ThumbnailSizeChanged;

            tknSrc_LoadThumb?.Cancel();
            tknSrc_LoadThumb?.Dispose();
            while (tknSrc_LoadThumb != null) { await Task.Delay(100); }

            Dispatcher.Invoke(() => ObjectList.Clear());

            Setting.LastPath = CurrentPath;
            if (WindowState != WindowState.Maximized) {
                Setting.LastWindowSize = new Size(Width, Height);
            }

            //now really close
            reallyClose = true;
            Dispatcher.BeginInvoke(new Action(() => Close()));
        }

        private void ThumbnailSizeChanged(object sender, PropertyChangedEventArgs e) {
            if (PropertyChanged == null) return;
            if (e.PropertyName == nameof(Setting.ThumbnailSize.Item1))
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbRealWidth)));
            if (e.PropertyName == nameof(Setting.ThumbnailSize.Item2))
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbRealHeight)));
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

        private void callback_AddToImageList(ObjectInfo objInfo) {
            //exclude non-image items in immersion mode
            if (Setting.ImmersionMode && objInfo.SourcePaths == null) {
                UpdateSourcePaths(objInfo);//update needed to exclude items that do not have thumbs
                if (objInfo.SourcePaths == null || objInfo.SourcePaths.Length == 0)
                    return;
            }
            ObjectList.Add(objInfo);
        }

        internal void preRefreshActions() {
            //save scrollbar position
            Dispatcher.Invoke(() => scrollPosition(virWrapPanel.ScrollOwner.VerticalOffset));
            //try to free resources
            foreach (var objInfo in ObjectList) {
                objInfo.ImageSource = null;
            }
            //clear list
            ObjectList.Clear();
            //reset scroll
            Dispatcher.Invoke(() => virWrapPanel.ScrollOwner.ScrollToTop());
        }

        private readonly Dictionary<string, double> scrollPositions = new Dictionary<string, double>();
        /// <summary>
        /// Save the current or set the last scroll position.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private void scrollPosition(double? value = null) {
            var path = CurrentPath;
            if (string.IsNullOrEmpty(path)) return;

            if (value.HasValue)//save
                scrollPositions[path] = value.Value;
            else if (scrollPositions.ContainsKey(path))//set
                virWrapPanel.ScrollOwner.ScrollToVerticalOffset(scrollPositions[path]);
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
                    tknSrc_LoadThumb?.Dispose();
                    Monitor.Enter(lock_LoadThumb);
                    tknSrc_LoadThumb = new CancellationTokenSource();
                    preRefreshActions();
                    CurrentPath = objInfo.FileSystemPath;
                    foreach (var childInfo in new DirectoryInfo(objInfo.FileSystemPath).EnumerateFileSystemInfos()) {
                        if (tknSrc_LoadThumb?.IsCancellationRequested == true) return;

                        var flag = Helpers.GetPathType(childInfo);
                        callback_AddToImageList(new ObjectInfo(childInfo.FullName, flag) {
                            FileName = childInfo.Name,
                        });
                    }
                    Dispatcher.Invoke(() => scrollPosition());
                }
                finally {
                    tknSrc_LoadThumb.Dispose();
                    tknSrc_LoadThumb = null;
                    Monitor.Exit(lock_LoadThumb);
                }
            }
            else if (objInfo.Flags.HasFlag(FileFlags.Image)) {
                //plain image file or image inside archive -> open viewer
                //using a new ObjectInfo to avoid confusion and reduce chance of holding ImageSource
                var oi = new ObjectInfo(objInfo.FileSystemPath, objInfo.Flags) {
                    FileName = objInfo.FileName
                };
                Dispatcher.Invoke(() => {
                    if (viewWin == null)
                        new ViewWindow(this) { ObjectInfo = oi }.Show();
                    else
                        viewWin.ObjectInfo = oi;
                });
            }
            else if (objInfo.Flags.HasFlag(FileFlags.Archive)) {
                //archive itself -> extract and load thumbs
                try {
                    tknSrc_LoadThumb?.Cancel();
                    tknSrc_LoadThumb?.Dispose();
                    Monitor.Enter(lock_LoadThumb);
                    tknSrc_LoadThumb = new CancellationTokenSource();
                    preRefreshActions();
                    CurrentPath = objInfo.FileSystemPath;
                    LoadFile(new LoadOptions(objInfo.FileSystemPath) {
                        Flags = objInfo.Flags,
                        LoadImage = true,
                        DecodeSize = (SizeInt)Setting.ThumbnailSize,
                        CldInfoCallback = callback_AddToImageList,
                    }, tknSrc_LoadThumb);
                    Dispatcher.Invoke(() => scrollPosition());
                    //postRefreshActions();
                }
                finally {
                    tknSrc_LoadThumb.Dispose();
                    tknSrc_LoadThumb = null;
                    Monitor.Exit(lock_LoadThumb);
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
                        App.ContextMenuWin.MainWin = this;
                        App.ContextMenuWin.ParentWindow = this;
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

        #endregion

    }
}
