using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Threading;
using Timer = System.Timers.Timer;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace ZipImageViewer
{
    public partial class Thumbnail : UserControl
    {
        public ObjectInfo ObjectInfo {
            get { return (ObjectInfo)GetValue(ObjectInfoProperty); }
            set { SetValue(ObjectInfoProperty, value); }
        }
        public static readonly DependencyProperty ObjectInfoProperty =
            DependencyProperty.Register("ObjectInfo", typeof(ObjectInfo), typeof(Thumbnail), new PropertyMetadata(null));

        
        private Timer CycleTimer;

        public static ImageSource DefaultThumb => FontAwesome.WPF.ImageAwesome.CreateImageSource(FontAwesome.WPF.FontAwesomeIcon.Image, Brushes.LightGray);

        public Thumbnail() {
            InitializeComponent();
        }

        private void TN_Loaded(object sender, RoutedEventArgs e) {
            if (ObjectInfo.ImageSources.Count < 2) return;

            //cycle thumbnails when more than one exists
            CycleTimer = new Timer(new Random().Next(3000, 5000));
            CycleTimer.Elapsed += (o1, e1) => {
                //Thread.Sleep(new Random().Next(0, 500));
                Dispatcher.Invoke(() => {
                    //fade out
                    IM1.AnimateDoubleCubicEase(OpacityProperty, 0d, 500, EasingMode.EaseIn);
                });
                Thread.Sleep(500);
                Dispatcher.Invoke(() => {
                    //change image
                    var currentIdx = ObjectInfo.ImageSources.IndexOf(IM1.Source);
                    var nextIdx = currentIdx == ObjectInfo.ImageSources.Count - 1 ? 0 : currentIdx + 1;
                    var binding = new Binding() {
                        ElementName = nameof(TN),
                        Path = new PropertyPath($"ObjectInfo.ImageSources[{nextIdx}]"),
                        Mode = BindingMode.OneWay,
                    };
                    BindingOperations.SetBinding(IM1, Image.SourceProperty, binding);
                    //fade in
                    IM1.AnimateDoubleCubicEase(OpacityProperty, 1d, 500, EasingMode.EaseOut);
                });
                
            };
            CycleTimer.Start();
        }
    }
}
