using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using SizeInt = System.Drawing.Size;
using System.Windows.Threading;
using System.Threading;

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


        //public Visibility FlagIconVisibility {
        //    get { return (Visibility)GetValue(FlagIconVisibilityProperty); }
        //    set { SetValue(FlagIconVisibilityProperty, value); }
        //}
        //public static readonly DependencyProperty FlagIconVisibilityProperty =
        //    DependencyProperty.Register("FlagIconVisibility", typeof(Visibility), typeof(Thumbnail), new PropertyMetadata(Visibility.Visible));


        //public bool HasImage => thumbImageSource != App.fa_meh &&
        //                        thumbImageSource != App.fa_exclamation &&
        //                        thumbImageSource != App.fa_file &&
        //                        thumbImageSource != App.fa_folder &&
        //                        thumbImageSource != App.fa_archive &&
        //                        thumbImageSource != App.fa_image;

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

        private MainWindow mainWin;
        private DispatcherTimer cycleTimer;

        public Thumbnail() {
            InitializeComponent();
#if DEBUG
            IM1.SetBinding(ToolTipProperty, new Binding() {
                ElementName = @"TN",
                Path = new PropertyPath($@"{nameof(ObjectInfo)}.{nameof(ObjectInfo.DebugInfo)}"),
                Mode = BindingMode.OneWay,
            });
            ToolTipService.SetShowDuration(IM1, 20000);
#endif
            thumbTransAnimCount = Resources.Keys.Cast<string>().Count(k => k.StartsWith(@"SB_ThumbTrans_")) / 2;

        }


        private void ThumbTransAnimOut_Completed(object sender, EventArgs e) {
            thumbImageSource = nextSource;
            nextSource = null;
            if (PropertyChanged != null) {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbImageSource)));
                //PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(HasImage)));
            }
            GR1.BeginStoryboard(thumbTransAnimIn);
        }

        private void TN_Loaded(object sender, RoutedEventArgs e) {
            cycleTimer = new DispatcherTimer(DispatcherPriority.Normal, Application.Current.Dispatcher);
            cycleTimer.Tick += cycleImageSource;

            mainWin = (MainWindow)Window.GetWindow(this);
            
            var objInfo = ObjectInfo;
            var rm = mainWin.AuxVisibility == Visibility.Collapsed;
            Task.Run(() => {
                if (objInfo.SourcePaths == null) {//non-null indicate SourcePaths has already been updated
                    Helpers.UpdateSourcePaths(objInfo);
                }
            }).ContinueWith(t => Dispatcher.Invoke(() => cycleImageSource(null, null)));
        }

        private void TN_Unloaded(object sender, RoutedEventArgs e) {
            //ThumbImageSource = null;
            //if (ObjectInfo != null) {
            //    ObjectInfo.ImageSources = null;
            //    ObjectInfo = null;
            //}
            //nextSource = null;
            //if (IM1 != null) {
            //    IM1.Source = null;
            //    IM1.ToolTip = null;
            //    IM1 = null;
            //}
            //mask = null;
            cycleTimer.Stop();
            cycleTimer.Tick -= cycleImageSource;
        }

        private async void cycleImageSource(object sender, EventArgs e) {
            var tn = this;
            tn.cycleTimer.Stop();

            if (!tn.IsLoaded) return; //dont do anything before or after the lifecycle
            if (tn.ObjectInfo == null) return;

            var cycle = false;
            if (tn.ObjectInfo.SourcePaths?.Length > 1) {
                tn.thumbIndex = tn.thumbIndex == tn.ObjectInfo.SourcePaths.Length - 1 ? 0 : tn.thumbIndex + 1;
                cycle = true;
            }
            else
                tn.thumbIndex = 0;


//#if DEBUG
//            Console.WriteLine($"Cycling {tn.ObjectInfo.VirtualPath}... Delay {delay} ms.");
//#endif

            //var objInfo = ObjectInfo;
            //ThreadPool.QueueUserWorkItem(s => {
            //    tn.ThumbImageSource = Helpers.GetImageSource(objInfo, tn.thumbIndex, (SizeInt)Setting.ThumbnailSize);

            //    Dispatcher.Invoke(() => {
            //        if (!tn.IsLoaded || !cycle) return;
            //        var delay = mainWin.ThumbChangeDelay;
            //        tn.cycleTimer.Interval = TimeSpan.FromMilliseconds(delay);
            //        tn.cycleTimer.Start();
            //    });

            //});

            tn.ThumbImageSource = await Helpers.GetImageSource(tn.ObjectInfo, tn.thumbIndex, true);

            if (!tn.IsLoaded || !mainWin.IsLoaded || !cycle) return; //dont do anything before or after the lifecycle

            var delay = mainWin.ThumbChangeDelay;

            tn.cycleTimer.Interval = TimeSpan.FromMilliseconds(delay);
            tn.cycleTimer.Start();
        }
    }
}
