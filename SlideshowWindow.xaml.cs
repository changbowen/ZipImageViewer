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
using SizeInt = System.Drawing.Size;
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


        public SlideAnimConfig AnimConfig {
            get { return (SlideAnimConfig)GetValue(AnimConfigProperty); }
            set { SetValue(AnimConfigProperty, value); }
        }
        public static readonly DependencyProperty AnimConfigProperty =
            DependencyProperty.Register("AnimConfig", typeof(SlideAnimConfig), typeof(SlideshowWindow), new PropertyMetadata(null));


        public SlideshowWindow(ObjectInfo objInfo) {
            InitializeComponent();

            AnimConfig = new SlideAnimConfig(Setting.SlideAnimConfig);
            ObjectInfo = new ObjectInfo(objInfo.ContainerPath);
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

        private int index = -1;
        private DpiImage currImage;
        private Rect lastRect;

        private void SlideWin_Loaded(object sender, RoutedEventArgs e) {
            //init controls
            CB_Transition.ItemsSource = Enum.GetValues(typeof(SlideTransition));
            CB_Transition.SelectedItem = AnimConfig.Transition;

            //get list of images to use
            ObjectInfo.Flags = Helpers.GetPathType(new DirectoryInfo(ObjectInfo.FileSystemPath));
            Helpers.UpdateSourcePaths(ObjectInfo);

            //fullscreen
            Helpers.SwitchFullScreen(this, ref lastRect, true);

            //start
            AnimTick(null, null);
        }

        private void SlideWin_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            animTimer.Stop();
            Setting.SlideAnimConfig = AnimConfig;//only the last closed window setting is saved
        }

        private void Btn_Preset_Click(object sender, RoutedEventArgs e) {
            switch (AnimConfig.Transition) {
                case SlideTransition.KenBurns:
                    AnimConfig.FadeInDuration = new TimeSpan(0, 0, 2);
                    AnimConfig.FadeOutDuration = new TimeSpan(0, 0, 2);
                    break;
                case SlideTransition.Breath:
                    AnimConfig.FadeInDuration = TimeSpan.FromMilliseconds(1500);
                    AnimConfig.FadeOutDuration = TimeSpan.FromMilliseconds(1500);
                    break;
                case SlideTransition.Emerge:
                    AnimConfig.lastBool1 = ran.Next(2) == 0;
                    AnimConfig.FadeInDuration = new TimeSpan(0, 0, 2);
                    AnimConfig.FadeOutDuration = TimeSpan.FromMilliseconds(1500);
                    break;
            }
        }

        private void CB_Transition_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var tran = (SlideTransition)e.AddedItems[0];
            AnimConfig.Transition = tran;
        }


        private void B_ControlPanel_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) {
            var border = (Border)sender;
            border.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
        }


        private async void AnimTick(object sender, EventArgs e) {
            animTimer.Stop();
            if (!IsLoaded) return;

            index = index == ObjectInfo.SourcePaths.Length - 1 ? 0 : index + 1;
            
            //convert screen size to physical size
            var dpi = VisualTreeHelper.GetDpi(this);
            var nextSrc = await Helpers.GetImageSourceAsync(ObjectInfo, index,
                decodeSize: new SizeInt(Convert.ToInt32(canvas.ActualWidth * dpi.DpiScaleX * AnimConfig.ResolutionScale),
                                        Convert.ToInt32(canvas.ActualHeight * dpi.DpiScaleY * AnimConfig.ResolutionScale)));

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
                animTimer.Interval = AnimateImage(currImage, new Size(canvas.ActualWidth, canvas.ActualHeight), AnimConfig);
            }
            else {
                animTimer.Interval = TimeSpan.FromMilliseconds(50);
            }

            animTimer.Start();
        }

    }
}
