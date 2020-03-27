using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static ZipImageViewer.LoadHelper;
using static ZipImageViewer.Helpers;

namespace ZipImageViewer
{
    public partial class ContextMenuWindow : RoundedWindow, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public MainWindow MainWin { get; set; }

        public ContextMenuWindow() {
            InitializeComponent();
        }

        private ObjectInfo objectInfo;
        public ObjectInfo ObjectInfo {
            get => objectInfo;
            set {
                if (objectInfo == value) return;
                objectInfo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ObjectInfo)));
                Task.Run(() => {
                    var imgInfo = getImageInfo(ObjectInfo);
                    Dispatcher.Invoke(() => ImageInfo = imgInfo);
                });
            }
        }


        public ImageInfo ImageInfo {
            get { return (ImageInfo)GetValue(ImageInfoProperty); }
            set { SetValue(ImageInfoProperty, value); }
        }
        public static readonly DependencyProperty ImageInfoProperty =
            DependencyProperty.Register("ImageInfo", typeof(ImageInfo), typeof(ContextMenuWindow), new PropertyMetadata(null));


        private static ImageInfo getImageInfo(ObjectInfo objInfo) {
            if (objInfo == null) return null;
            var imgInfo = new ImageInfo();
            try {
                if (objInfo.Flags == (FileFlags.Archive | FileFlags.Image)) {
                    ExtractFile(objInfo.FileSystemPath, objInfo.FileName, (fileInfo, stream) => {
                        imgInfo.Created = fileInfo.CreationTime;
                        imgInfo.Modified = fileInfo.LastWriteTime;
                        imgInfo.FileSize = (long)fileInfo.Size;
                        UpdateImageInfo(stream, imgInfo);
                    });
                }
                else {
                    var fileInfo = new FileInfo(objInfo.FileSystemPath);
                    imgInfo.Created = fileInfo.CreationTime;
                    imgInfo.Modified = fileInfo.LastWriteTime;
                    if (!fileInfo.Attributes.HasFlag(FileAttributes.Directory)) {
                        imgInfo.FileSize = fileInfo.Length;
                        if (objInfo.Flags.HasFlag(FileFlags.Image)) {
                            using (var stream = new FileStream(objInfo.FileSystemPath, FileMode.Open, FileAccess.Read)) {
                                UpdateImageInfo(stream, imgInfo);
                            }
                        }
                    }
                }
            }
            catch { }
            return imgInfo;
        }

        private void Menu_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            if (ObjectInfo == null) return;
            if (sender is Border border) {
                switch (border.Name) {
                    case nameof(B_OpenWithDefaultApp):
                        Run("explorer", ObjectInfo.FileSystemPath);
                        break;
                    case nameof(B_OpenInExplorer):
                        Run("explorer", $"/select, \"{ObjectInfo.FileSystemPath}\"");
                        break;
                    case nameof(B_OpenInNewWindow):
                        if (ObjectInfo.Flags.HasFlag(FileFlags.Image)) {
                            MainWin?.LoadPath(ObjectInfo);
                        }
                        else if (ObjectInfo.Flags.HasFlag(FileFlags.Directory) ||
                                 ObjectInfo.Flags.HasFlag(FileFlags.Archive)) {
                            var win = new MainWindow {
                                InitialPath = ObjectInfo.FileSystemPath
                            };
                            win.Show();
                        }
                        break;
                    case nameof(B_CacheFirst):
                        CacheHelper.CachePath(ObjectInfo.FileSystemPath, true);
                        break;
                    case nameof(B_CacheAll):
                        CacheHelper.CachePath(ObjectInfo.FileSystemPath, false);
                        break;
                    case nameof(B_Slideshow):
                        var sldWin = new SlideshowWindow(ObjectInfo.ContainerPath);
                        sldWin.Show();
                        break;
                }
            }
            else if (sender is ContentPresenter cp && cp.Content is ObservableObj obsObj) {
                Run(obsObj.Str2, CustomCmdArgsReplace(obsObj.Str3, ObjectInfo));
            }

            Close();
            e.Handled = true;
        }

        private void CTMWin_FadedOut(object sender, RoutedEventArgs e) {
            ObjectInfo = null;
            ImageInfo = null;
            MainWin = null;
        }
    }
}
