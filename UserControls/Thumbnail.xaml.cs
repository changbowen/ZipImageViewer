using System;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Collections.Specialized;

namespace ZipImageViewer
{
    public partial class Thumbnail : UserControl, INotifyPropertyChanged
    {
        public ObjectInfo ObjectInfo {
            get { return (ObjectInfo)GetValue(ObjectInfoProperty); }
            set { SetValue(ObjectInfoProperty, value); }
        }
        public static readonly DependencyProperty ObjectInfoProperty =
            DependencyProperty.Register("ObjectInfo", typeof(ObjectInfo), typeof(Thumbnail), new PropertyMetadata(null, (o, e)=> {
                if (e.NewValue == null) return;
                var tn = (Thumbnail)o;
                var objInfo = (ObjectInfo)e.NewValue;
                objInfo.ImageSources.CollectionChanged += (o1, e1) => {
                    var col = (ObservableCollection<ImageSource>)o1;
                    switch (e1.Action) {
                        case NotifyCollectionChangedAction.Add:
                            if (col.Count == 1) tn.ThumbSource = col[0];
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            if (col.Count == 0) tn.ThumbSource = fa_meh;
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            tn.ThumbSource = fa_meh;
                            break;
                    }
                };
            }));


        private static readonly ImageSource fa_meh = FontAwesome5.ImageAwesome.CreateImageSource(
            FontAwesome5.EFontAwesomeIcon.Solid_Meh,
            new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)));

        //private Timer CycleTimer = new Timer() { AutoReset = false };

        public event PropertyChangedEventHandler PropertyChanged;
        
        
        private ImageSource thumbSource = fa_meh;
        public ImageSource ThumbSource {
            get => thumbSource;
            set {
                if (thumbSource == value) return;
                Dispatcher.Invoke(() => {
                    //fade out
                    IM1.AnimateDoubleCubicEase(OpacityProperty, 0d, 500, EasingMode.EaseIn,
                        completed: (o1, e1) => {
                            thumbSource = value;
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbSource)));
                        });
                    //fade in
                    IM1.AnimateDoubleCubicEase(OpacityProperty, 1d, 500, EasingMode.EaseOut, begin: 520);
                });
            }
        }

        public Thumbnail() {
            InitializeComponent();
        }

        private void TN_Loaded(object sender, RoutedEventArgs e) {
            cycleImageSource();
        }

        private void cycleImageSource() {
            if (!IsLoaded) return; //dont do anything before or after the lifecycle
            Console.WriteLine(ObjectInfo.FileSystemPath);

            if (ObjectInfo.ImageSources.Count > 0 && ThumbSource == fa_meh)
                ThumbSource = ObjectInfo.ImageSources[0];
            else if (ObjectInfo.ImageSources.Count == 1 && ObjectInfo.Flags.HasFlag(FileFlags.Image)) {
                ThumbSource = ObjectInfo.ImageSources[0];
                return;
            }
            else if (ObjectInfo.ImageSources.Count > 0) {
                var currentIdx = ObjectInfo.ImageSources.IndexOf(IM1.Source);
                var nextIdx = currentIdx == ObjectInfo.ImageSources.Count - 1 ? 0 : currentIdx + 1;
                ThumbSource = ObjectInfo.ImageSources[nextIdx];
            }

            if (!IsLoaded) return; //dont do anything before or after the lifecycle
            Task.Run(() => {
                Thread.Sleep(App.MainWin.ThumbChangeDelay);
                Dispatcher.Invoke(cycleImageSource);
            });
        }
    }
}
