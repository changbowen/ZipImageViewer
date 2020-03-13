using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;
using System.ComponentModel;
using System.Threading;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Threading;
using System.Data;
using Path = System.IO.Path;
using SizeInt = System.Drawing.Size;
using static ZipImageViewer.Helpers;
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
        private readonly object lock_LoadThumb = new object();

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

            virWrapPanel = GetVisualChild<VirtualizingWrapPanel>(TV1);

            DpiScale = VisualTreeHelper.GetDpi(this);
            Setting.ThumbnailSize.PropertyChanged += ThumbnailSizeChanged;

            var view = (ListCollectionView)((CollectionViewSource)FindResource("ObjectListViewSource")).View;
            view.CustomSort = new Helpers.ObjectInfoSorter();

            //load last path or open dialog
            if (InitialPath?.Length > 0)
                Task.Run(() => LoadPath(InitialPath));
            else if (Setting.LastPath?.Length > 0)
                Task.Run(() => LoadPath(Setting.LastPath));
            else
                openFolderPrompt();
        }

        private void MainWin_Closed(object sender, EventArgs e) {
            ShutdownCheck();
        }

        private bool reallyClose = false;
        private async void MainWin_Closing(object sender, CancelEventArgs e) {
            if (reallyClose) return;

            //start cleaning up
            e.Cancel = true;
            Setting.ThumbnailSize.PropertyChanged -= ThumbnailSizeChanged;

            tknSrc_LoadThumb?.Cancel();
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

        private void MainWin_KeyDown(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.PageDown:
                    virWrapPanel.ScrollOwner.PageDown();
                    break;
                case Key.PageUp:
                    virWrapPanel.ScrollOwner.PageUp();
                    break;
            }
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
            OpenFolderDialog(this, path => Task.Run(() => LoadPath(path)));
        }

        /// <summary>
        /// This needs to be synchronous for the cancallation to work.
        /// </summary>
        private void callback_AddToImageList(ObjectInfo objInfo) {
            //exclude non-image items in immersion mode
            if (Setting.ImmersionMode && objInfo.SourcePaths == null) {
                objInfo.SourcePaths = GetSourcePaths(objInfo);//update needed to exclude items that do not have thumbs
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
            LoadPath(new ObjectInfo(path, GetPathType(path)), viewWin);
        }

        /// <summary>
        /// Display file system objects in the path as thumbnails, or open viewer depending on the file type and parameters.
        /// Main open logic is set here.
        /// Support cancellation. Used in Task.
        /// </summary>
        internal void LoadPath(ObjectInfo objInfo, ViewWindow viewWin = null) {
            // action based on flags
            if (objInfo.Flags.HasFlag(FileFlags.Directory)) {
                //directory -> load thumbs
                try {
                    tknSrc_LoadThumb?.Cancel();
                    Monitor.Enter(lock_LoadThumb);
                    tknSrc_LoadThumb = new CancellationTokenSource();
                    preRefreshActions();
                    CurrentPath = objInfo.FileSystemPath;
                    foreach (var childInfo in new DirectoryInfo(objInfo.FileSystemPath).EnumerateFileSystemInfos()) {
                        if (tknSrc_LoadThumb?.IsCancellationRequested == true) return;

                        var flag = GetPathType(childInfo);
                        callback_AddToImageList(new ObjectInfo(childInfo.FullName, flag, childInfo.Name));
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
                Dispatcher.Invoke(() => {
                    if (viewWin == null)
                        new ViewWindow(objInfo.ContainerPath, objInfo.FileName, this).Show();
                    else
                        viewWin.ViewPath = (objInfo.ContainerPath, objInfo.FileName);
                });
            }
            else if (objInfo.Flags.HasFlag(FileFlags.Archive)) {
                //archive itself -> extract and load thumbs
                try {
                    tknSrc_LoadThumb?.Cancel();
                    Monitor.Enter(lock_LoadThumb);
                    tknSrc_LoadThumb = new CancellationTokenSource();
                    preRefreshActions();
                    CurrentPath = objInfo.FileSystemPath;
                    ExtractZip(new LoadOptions(objInfo.FileSystemPath) {
                        Flags = objInfo.Flags,
                        LoadImage = true,
                        DecodeSize = (SizeInt)Setting.ThumbnailSize,
                        CldInfoCallback = callback_AddToImageList,
                    }, tknSrc_LoadThumb);
                    Dispatcher.Invoke(() => scrollPosition());
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

        private void Sidebar_Click(object sender, MouseButtonEventArgs e) {
            switch (((FrameworkElement)sender).Name) {
                case nameof(HY_Open):
                    openFolderPrompt();
                    break;
                case nameof(HY_CacheFirst):
                    cacheView(false);
                    break;
                case nameof(HY_CacheAll):
                    cacheView(true);
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

        private void cacheView(bool cacheAll) {
            var bw = new BlockWindow(this) {
                MessageTitle = GetRes("msg_Processing")
            };
            //callback used to update progress
            Action<string, int, int> cb = (path, i, count) => {
                var p = (int)Math.Floor((double)i / count * 100);
                Dispatcher.Invoke(() => {
                    bw.Percentage = p;
                    bw.MessageBody = path;
                    if (bw.Percentage == 100) bw.MessageTitle = GetRes("ttl_OperationComplete");
                });
            };

            //work thread
            bw.Work = () => {
                tknSrc_LoadThumb?.Cancel();
                while (tknSrc_LoadThumb != null) {
                    Thread.Sleep(200);
                }
                preRefreshActions();
                CacheFolder(CurrentPath, ref bw.tknSrc_Work, bw.lock_Work, cb, cacheAll);
                Task.Run(() => LoadPath(CurrentPath));
            };
            bw.FadeIn();
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
