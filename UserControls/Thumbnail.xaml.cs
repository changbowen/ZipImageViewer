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


        public Visibility FlagIconVisibility {
            get { return (Visibility)GetValue(FlagIconVisibilityProperty); }
            set { SetValue(FlagIconVisibilityProperty, value); }
        }
        public static readonly DependencyProperty FlagIconVisibilityProperty =
            DependencyProperty.Register("FlagIconVisibility", typeof(Visibility), typeof(Thumbnail), new PropertyMetadata(Visibility.Visible));


        public bool HasError => thumbImageSource == App.fa_meh || thumbImageSource == App.fa_exclamation;

        public event PropertyChangedEventHandler PropertyChanged;

        private int thumbIndex = -1;

        private int thumbTransAnimCount;
        private string thumbTransAnimName;
        private Storyboard thumbTransAnimOut => (Storyboard)FindResource($"{thumbTransAnimName}_Out");
        private Storyboard thumbTransAnimIn => (Storyboard)FindResource($"{thumbTransAnimName}_In");

        private ImageSource nextSource;

        private ImageSource thumbImageSource = App.fa_spinner;
        public ImageSource ThumbImageSource {
            get => thumbImageSource;
            set {
                if (thumbImageSource == value) return;
                nextSource = value;
                if (thumbImageSource == App.fa_spinner)
                    //use simpler animation for initial animation to reduce performance hit
                    thumbTransAnimName = @"SB_ThumbTransInit";
                else
                    thumbTransAnimName = $@"SB_ThumbTrans_{App.Random.Next(0, thumbTransAnimCount)}";

                Dispatcher.Invoke(() => {
                    if (IsLoaded) GR1.BeginStoryboard(thumbTransAnimOut);
                    //fade out
                    //IM1.AnimateDoubleCubicEase(OpacityProperty, 0d, 500, EasingMode.EaseIn,
                    //    completed: (o1, e1) => {
                    //        thumbImageSource = value;
                    //        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbImageSource)));
                    //    });
                    //fade in
                    //IM1.AnimateDoubleCubicEase(OpacityProperty, 1d, 500, EasingMode.EaseOut, begin: 520);
                });
            }
        }


        public Thumbnail() {
            InitializeComponent();
            
            thumbTransAnimCount = Resources.Keys.Cast<string>().Count(k => k.StartsWith(@"SB_ThumbTrans_")) / 2;
        }

        private void ThumbTransAnimOut_Completed(object sender, EventArgs e) {
            thumbImageSource = nextSource;
            nextSource = null;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbImageSource)));
            GR1.BeginStoryboard(thumbTransAnimIn);
        }

        private void TN_Loaded(object sender, RoutedEventArgs e) {
            cycleImageSource();
        }

        private void TN_Unloaded(object sender, RoutedEventArgs e) {
            ThumbImageSource = null;
            ObjectInfo.ImageSources = null;
            ObjectInfo = null;
            nextSource = null;
            IM1.Source = null;
            IM1.ToolTip = null;
            IM1 = null;
            mask = null;
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
                try {
                    Thread.Sleep(App.MainWin.ThumbChangeDelay);
                    Dispatcher.Invoke(cycleImageSource);
                }
                catch (TaskCanceledException) { }
            });
        }
    }
}
