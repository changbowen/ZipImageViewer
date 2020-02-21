using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static ZipImageViewer.SlideshowHelper;

namespace ZipImageViewer
{
    /// <summary>
    /// Interaction logic for SlideshowWindow.xaml
    /// </summary>
    public partial class SlideshowWindow : BorderlessWindow
    {
        private readonly ObjectInfo ObjectInfo;

        private readonly DispatcherTimer animTimer;
        private ImageSource currSource;
        private ImageSource nextSource;

        public SlideshowWindow(MainWindow win, ObjectInfo objInfo) {
            InitializeComponent();
            mainWin = win;

            ObjectInfo = new ObjectInfo(objInfo.ContainerPath);
            animTimer = new DispatcherTimer(DispatcherPriority.Normal, Application.Current.Dispatcher);
            animTimer.Tick += AnimTick;
        }



        private readonly MainWindow mainWin;
        private int index = -1;
        private DpiImage currImage;

        private void SlideWin_Loaded(object sender, RoutedEventArgs e) {
            ObjectInfo.Flags = Helpers.GetPathType(new DirectoryInfo(ObjectInfo.FileSystemPath));
            Helpers.UpdateSourcePaths(ObjectInfo);
            
            AnimTick(null, null);
        }


        private void SlideWin_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            animTimer.Stop();
        }


        private async void AnimTick(object sender, EventArgs e) {
            animTimer.Stop();
            var animCfg = new SlideAnimConfig() {
                XPanDistanceR = 0.5,
                YPanDistanceR = 0.5,
            };

            index = index == ObjectInfo.SourcePaths.Length - 1 ? 0 : index + 1;
            var nextSrc = await Helpers.GetImageSource(ObjectInfo, index, false);

            if (nextSrc != null) {
                //currSource = nextSrc;
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
                animTimer.Interval = Anim_KBE(currImage, new Size(canvas.ActualWidth, canvas.ActualHeight), animCfg);
            }
            else {
                animTimer.Interval = TimeSpan.FromMilliseconds(50);
            }

            animTimer.Start();
        }

    }
}
