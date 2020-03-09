using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SizeInt = System.Drawing.Size;
using static ZipImageViewer.Helpers;
using static ZipImageViewer.SlideshowHelper;
using static ZipImageViewer.LoadHelper;

namespace ZipImageViewer
{
    public partial class SlideshowWindow : BorderlessWindow
    {
        private readonly string basePath;
        private readonly DispatcherTimer animTimer;
        private readonly SlideAnimConfig animConfig;
        //public SlideAnimConfig AnimConfig {
        //    get { return (SlideAnimConfig)GetValue(AnimConfigProperty); }
        //    set { SetValue(AnimConfigProperty, value); }
        //}
        //public static readonly DependencyProperty AnimConfigProperty =
        //    DependencyProperty.Register("AnimConfig", typeof(SlideAnimConfig), typeof(SlideshowWindow), new PropertyMetadata(null));

        /// <summary>
        /// path will be used to get images from.
        /// </summary>
        public SlideshowWindow(string path) {
            InitializeComponent();

            basePath = path;

            animConfig = Setting.SlideAnimConfig;
            animTimer = new DispatcherTimer(DispatcherPriority.Normal, Application.Current.Dispatcher);
            animTimer.Tick += AnimTick;

            B_ControlPanel.Loaded += B_ControlPanel_Loaded;
        }

        private void B_ControlPanel_Loaded(object sender, RoutedEventArgs e) {
            //simple useless animation that cannot be done easily in xaml achieving the same effect
            var b = (Border)sender;
            var a = new DoubleAnimation(0d, new Duration(new TimeSpan(0, 0, 1))) {
                BeginTime = new TimeSpan(0, 0, 3),
                FillBehavior = FillBehavior.Stop,
            };
            a.Completed += (o1, e1) => {
                b.Opacity = 0d;
                b.Loaded -= B_ControlPanel_Loaded;
                a = null;
            };
            b.BeginAnimation(OpacityProperty, a);
        }

        private DpiImage currImage;
        private Rect lastRect;

        private (int objIdx, int subIdx) index = (0, 0);
        private ObjectInfo[] objectList;

        private void SlideWin_Loaded(object sender, RoutedEventArgs e) {
            //get image to use
            objectList = GetAll(basePath)?.ToArray();

            if (objectList?.Length == 0) {
                MessageBox.Show(GetRes("msg_NoImageFound", basePath), null, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Close();
                return;
            }
            //minimize other app windows
            foreach (var win in Application.Current.Windows.Cast<Window>()) {
                if (win is MainWindow || win is ViewWindow) win.WindowState = WindowState.Minimized;
            }

            //fullscreen
            SwitchFullScreen(this, ref lastRect, true);
            Topmost = true;

            //start
            AnimTick(null, null);
        }

        private void SlideWin_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            animTimer.Stop();
            Topmost = false;
        }

        private void SlideWin_Closed(object sender, EventArgs e) {
            //reopen app windows
            foreach (var win in Application.Current.Windows.Cast<Window>()) {
                if ((win is MainWindow || win is ViewWindow) && win.WindowState == WindowState.Minimized) win.WindowState = WindowState.Normal;
            }
            ShutdownCheck();
        }

        private void Btn_Preset_Click(object sender, RoutedEventArgs e) {
            switch (animConfig.Transition) {
                case SlideTransition.KenBurns:
                    animConfig.FadeInDuration = new TimeSpan(0, 0, 2);
                    animConfig.FadeOutDuration = new TimeSpan(0, 0, 2);
                    animConfig.ImageDuration = new TimeSpan(0, 0, 7);
                    animConfig.XPanDistanceR = 0.5;
                    animConfig.YPanDistanceR = 0.5;
                    animConfig.YPanDownOnly = true;
                    break;
                case SlideTransition.Breath:
                    animConfig.FadeInDuration = TimeSpan.FromMilliseconds(1500);
                    animConfig.FadeOutDuration = TimeSpan.FromMilliseconds(1500);
                    break;
                case SlideTransition.Emerge:
                    animConfig.lastBool1 = ran.Next(2) == 0;
                    animConfig.FadeInDuration = new TimeSpan(0, 0, 2);
                    animConfig.FadeOutDuration = TimeSpan.FromMilliseconds(1500);
                    break;
            }
        }


        private void B_ControlPanel_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) {
            var border = (Border)sender;
            border.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
        }


        private async void AnimTick(object sender, EventArgs e) {
            animTimer.Stop();
            if (!IsLoaded) return;

            ImageSource nextSrc = null;
            
            //convert screen size to physical size
            var dpi = VisualTreeHelper.GetDpi(this);
            var decodeSize = new SizeInt(Convert.ToInt32(canvas.ActualWidth * dpi.DpiScaleX * animConfig.ResolutionScale),
                                         Convert.ToInt32(canvas.ActualHeight * dpi.DpiScaleY * animConfig.ResolutionScale));
            //calculate index
            var currObj = objectList[index.objIdx];
            switch (currObj.Flags) {
                case FileFlags.Image:
                    nextSrc = await GetImageSourceAsync(currObj.FileSystemPath, decodeSize);
                    index.objIdx = index.objIdx == objectList.Length - 1 ? 0 : index.objIdx + 1;
                    break;
                case FileFlags.Archive:
                    if (currObj.SourcePaths == null)
                        currObj.SourcePaths = await GetSourcePathsAsync(currObj);
                    nextSrc = await GetImageSourceAsync(currObj, sourcePathIdx: index.subIdx, decodeSize: decodeSize);
                    index.subIdx++;
                    if (index.subIdx >= currObj.SourcePaths?.Length) {
                        index.subIdx = 0;
                        index.objIdx = index.objIdx == objectList.Length - 1 ? 0 : index.objIdx + 1;
                    }
                    break;
            }

            if (nextSrc != null) {
                //switch target
                if (currImage == IM0) {
                    Panel.SetZIndex(IM0, 8);
                    Panel.SetZIndex(IM1, 9);
                    currImage = IM1;
                }
                else {
                    Panel.SetZIndex(IM0, 9);
                    Panel.SetZIndex(IM1, 8);
                    currImage = IM0;
                }

                currImage.Source = nextSrc;
                animTimer.Interval = AnimateImage(currImage, new Size(canvas.ActualWidth, canvas.ActualHeight), animConfig);
            }
            else {
                animTimer.Interval = TimeSpan.FromMilliseconds(50);
            }

            animTimer.Start();
        }

    }
}
