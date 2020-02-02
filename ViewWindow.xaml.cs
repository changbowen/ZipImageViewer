using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using FontAwesome.WPF;
using static ZipImageViewer.ExtentionMethods;

namespace ZipImageViewer
{
    public class CenterConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                throw new ArgumentException("Two double values need to be passed in this order -> totalWidth, width", nameof(values));

            var totalWidth = (double)values[0];
            var width = (double)values[1];
            return (totalWidth - width) / 2;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class ViewWindow : Window
    {
        public ObjectInfo ObjectInfo
        {
            get { return (ObjectInfo)GetValue(ObjectInfoProperty); }
            set { SetValue(ObjectInfoProperty, value); }
        }
        public static readonly DependencyProperty ObjectInfoProperty =
            Thumbnail.ObjectInfoProperty.AddOwner(typeof(ViewWindow));


        public bool Transforming
        {
            get { return (bool)GetValue(TransformingProperty); }
            set { SetValue(TransformingProperty, value); }
        }
        public static readonly DependencyProperty TransformingProperty =
            DependencyProperty.Register("Transforming", typeof(bool), typeof(ViewWindow), new PropertyMetadata(false));

        private double Scale = 1d;

        //public Point CenterPoint =>
        //    new Point((CA.ActualWidth - IM.ActualWidth) / 2, (CA.ActualHeight - IM.ActualHeight) / 2);

        public ViewWindow()
        {
            InitializeComponent();
        }

        private void ViewWindow_Loaded(object sender, RoutedEventArgs e) {
            scaleToCanvas();
            IM.AnimateDoubleCubicEase(OpacityProperty, 1d, 500, EasingMode.EaseOut);
        }

        private void ViewWin_MouseUp(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Right) Close();
        }

        private void transform(int ms, Size? newSize = null, Point? transPoint = null) {
            if (newSize.HasValue) {
                newSize = new Size(newSize.Value.Width, newSize.Value.Height);
                if (double.IsNaN(IM.Width) || double.IsNaN(IM.Height)) {
                    IM.Width = newSize.Value.Width;
                    IM.Height = newSize.Value.Height;
                }
                else {
                    IM.AnimateDoubleCubicEase(WidthProperty, newSize.Value.Width, ms, EasingMode.EaseOut);
                    IM.AnimateDoubleCubicEase(HeightProperty, newSize.Value.Height, ms, EasingMode.EaseOut);
                }
                Scale = newSize.Value.Width / IM.RealSize.Width;
                BM.Show($"{Scale:P1}");
            }
            if (transPoint.HasValue) {
                transPoint = new Point(transPoint.Value.X.RoundToMultiplesOf(IM.TransformFromDevice.M11),
                                       transPoint.Value.Y.RoundToMultiplesOf(IM.TransformFromDevice.M22));
                IM_TT.AnimateDoubleCubicEase(TranslateTransform.XProperty, transPoint.Value.X, ms, EasingMode.EaseOut);
                IM_TT.AnimateDoubleCubicEase(TranslateTransform.YProperty, transPoint.Value.Y, ms, EasingMode.EaseOut);
            }
            if (ms > 0) {
                var animBool = new BooleanAnimationUsingKeyFrames();
                animBool.KeyFrames.Add(new DiscreteBooleanKeyFrame(true, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                animBool.KeyFrames.Add(new DiscreteBooleanKeyFrame(false, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms))));
                BeginAnimation(TransformingProperty, animBool);
            }
        }


        private void scaleToCanvas(int ms = 400) {
            var uniSize = Helpers.UniformScale(IM.RealSize, new Size(CA.ActualWidth, CA.ActualHeight));
            transform(ms, uniSize, new Point(0d, 0d));
        }

        private void scaleCenterMouse(Point relativePos, Size targetSize, int ms = 400) {
            //the point ensures pixels around mouse point not moving
            transform(ms, targetSize,
                          new Point(IM_TT.X + (IM.Width / 2d - relativePos.X) * (targetSize.Width / IM.Width - 1d),
                                    IM_TT.Y + (IM.Height/ 2d - relativePos.Y) * (targetSize.Height/ IM.Height - 1d)));
        }


        private Point mouseCapturePoint;
        private Matrix existingTranslate;

        private void CA_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 1) {
                mouseCapturePoint = e.GetPosition(CA);
                existingTranslate = IM_TT.Value;
                CA.CaptureMouse();
            }
            else if (e.ClickCount == 2) {
                if (!IM.IsRealSize)
                    scaleCenterMouse(e.GetPosition(IM), IM.RealSize);
                else
                    scaleToCanvas();

                ////use different animation when image is large to reduce stutter
                //if (rs.Width / CA.ActualWidth < 1.8d && rs.Height / CA.ActualHeight < 1.8d)
                //    Transform(ms, tgtSize, tgtPoint);
                //else
                //    Task.Run(() => {
                //        Dispatcher.Invoke(() => IM.AnimateDoubleCubicEase(OpacityProperty, 0.01d, 100, EasingMode.EaseOut));
                //        Thread.Sleep(100);
                //        Dispatcher.Invoke(() => Transform(0, tgtSize, tgtPoint));
                //        Thread.Sleep(100);
                //        Dispatcher.Invoke(() => IM.AnimateDoubleCubicEase(OpacityProperty, 1d, 200, EasingMode.EaseOut), DispatcherPriority.Background);
                //    });
            }
        }

        private void CA_PreviewMouseMove(object sender, MouseEventArgs e) {
            if (!CA.IsMouseCaptured) return;
            transform(50, transPoint:
                new Point(existingTranslate.OffsetX + ((e.GetPosition(CA).X - mouseCapturePoint.X) * Scale * 2d).RoundToMultiplesOf(IM.TransformFromDevice.M11),
                          existingTranslate.OffsetY + ((e.GetPosition(CA).Y - mouseCapturePoint.Y) * Scale * 2d).RoundToMultiplesOf(IM.TransformFromDevice.M22)));
        }

        private void CA_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            CA.ReleaseMouseCapture();
        }


        private void CA_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            //if (Transforming) return;
            var scale = e.Delta > 0 ? 1.25d : 0.8d;
            scaleCenterMouse(e.GetPosition(IM), new Size(IM.Width * scale, IM.Height * scale), 80);
        }

        private void CA_PreviewKeyUp(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Left:
                case Key.Right:
                case Key.Space:
                    var ii = ObjectInfo;
                    var ol = App.MainWin.ObjectList;
                    var i = ol.IndexOf(ol[ii.VirtualPath]) + (e.Key == Key.Left ? -1 : 1);
                    if (i > -1 && i < ol.Count) {
                        //fade animation
                        IM.AnimateDoubleCubicEase(OpacityProperty, 0d, 200, EasingMode.EaseOut);

                        //load next or previous image
                        Task.Run(() => {
                            var next = ol[i];
                            App.MainWin.LoadFile(next.FileSystemPath, next.Flags,
                                isThumb: false,
                                fileNames: new[] {next.FileName},
                                objInfoCb: nii => Dispatcher.Invoke(() => ObjectInfo = nii));
                            Dispatcher.Invoke(() => {
                                IM.AnimateDoubleCubicEase(OpacityProperty, 1d, 500, EasingMode.EaseOut);
                                scaleToCanvas();
                            }, DispatcherPriority.ApplicationIdle);
                        });
                    }
                    else
                        BM.Show("No more!");
                    break;
            }
        }

    }
}
