using System;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Linq;

namespace ZipImageViewer
{
    public partial class Thumbnail : UserControl, INotifyPropertyChanged
    {
        public ObjectInfo ObjectInfo {
            get { return (ObjectInfo)GetValue(ObjectInfoProperty); }
            set { SetValue(ObjectInfoProperty, value); }
        }
        public static readonly DependencyProperty ObjectInfoProperty =
            DependencyProperty.Register("ObjectInfo", typeof(ObjectInfo), typeof(Thumbnail), new PropertyMetadata(null));


        //private Timer CycleTimer = new Timer() { AutoReset = false };

        public event PropertyChangedEventHandler PropertyChanged;

        private int thumbIndex = -1;


        private ImageSource thumbImageSource = App.fa_spinner;
        public ImageSource ThumbImageSource {
            get => thumbImageSource;
            set {
                if (thumbImageSource == value) return;
                Dispatcher.Invoke(() => {
                    //fade out
                    IM1.AnimateDoubleCubicEase(OpacityProperty, 0d, 500, EasingMode.EaseIn,
                        completed: (o1, e1) => {
                            thumbImageSource = value;
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbImageSource)));
                        });
                    //fade in
                    IM1.AnimateDoubleCubicEase(OpacityProperty, 1d, 500, EasingMode.EaseOut, begin: 520);
                });
            }
        }


        //public ImageSource ThumbImageSource {
        //    get { return (ImageSource)GetValue(ThumbImageSourceProperty); }
        //    set { SetValue(ThumbImageSourceProperty, value); }
        //}
        //public static readonly DependencyProperty ThumbImageSourceProperty =
        //    DependencyProperty.Register("ThumbImageSource", typeof(ImageSource), typeof(Thumbnail), new PropertyMetadata(fa_meh));


        public Thumbnail() {
            InitializeComponent();
        }

        private void TN_Loaded(object sender, RoutedEventArgs e) {
            cycleImageSource();
        }

        private void cycleImageSource() {
            if (!IsLoaded) return; //dont do anything before or after the lifecycle
            if (ObjectInfo.ImageSources == null || ObjectInfo.ImageSources.Length == 0) {
                ThumbImageSource = App.fa_meh;
                return;
            }
#if DEBUG
            Console.WriteLine(ObjectInfo.FileSystemPath);
#endif
            if (ObjectInfo.ImageSources.Length == 1 && ObjectInfo.Flags.HasFlag(FileFlags.Image)) {
                ThumbImageSource = ObjectInfo.ImageSources[0];
                return;
            }
            else if (ObjectInfo.ImageSources.Length > 0) {
                thumbIndex = thumbIndex == ObjectInfo.ImageSources.Length - 1 ? 0 : thumbIndex + 1;
                ThumbImageSource = ObjectInfo.ImageSources[thumbIndex];
            }

            if (!IsLoaded) return; //dont do anything before or after the lifecycle
            Task.Run(() => {
                Thread.Sleep(App.MainWin.ThumbChangeDelay);
                Dispatcher.Invoke(cycleImageSource);
            });
        }

        private void TN_Unloaded(object sender, RoutedEventArgs e) {
            ThumbImageSource = null;
            ObjectInfo.ImageSources = null;
            ObjectInfo = null;
            IM1.Source = null;
            IM1.ToolTip = null;
            IM1 = null;
            mask = null;
        }
    }
}
