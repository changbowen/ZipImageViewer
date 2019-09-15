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
    public class CenterConterter : IMultiValueConverter
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
        public ImageInfo ImageInfo
        {
            get { return (ImageInfo)GetValue(ImageInfoProperty); }
            set { SetValue(ImageInfoProperty, value); }
        }
        public static readonly DependencyProperty ImageInfoProperty =
            Thumbnail.ImageInfoProperty.AddOwner(typeof(ViewWindow));


        public bool Transforming
        {
            get { return (bool)GetValue(TransformingProperty); }
            set { SetValue(TransformingProperty, value); }
        }
        public static readonly DependencyProperty TransformingProperty =
            DependencyProperty.Register("Transforming", typeof(bool), typeof(ViewWindow), new PropertyMetadata(false));


        private double Scale = 1d;

        public Point CenterPoint =>
            new Point((CA.ActualWidth - IM.ActualWidth) / 2, (CA.ActualHeight - IM.ActualHeight) / 2);

        public ViewWindow()
        {
            InitializeComponent();
            IM.Opacity = 0d;
        }

        private void ViewWindow_Loaded(object sender, RoutedEventArgs e) {
            var uniSize = Helpers.UniformScaleUp(IM.RealSize.Width, IM.RealSize.Height, CA.ActualWidth, CA.ActualHeight);
            IM.Width = uniSize.Width;
            IM.Height = uniSize.Height;
            IM.AnimateDoubleCubicEase(OpacityProperty, 1d, 500, EasingMode.EaseOut);
//            Task.Run(() => {
//                Thread.Sleep(100);
//                Dispatcher.Invoke(() => {
//                    
//                }, DispatcherPriority.ApplicationIdle);
//            });
        }

        /*private void IM_TargetUpdated(object sender, DataTransferEventArgs e) {
            if (!IsLoaded) return; //avoid firing twice the first time
        }*/

        private void ScaleToCanvas() {
            var uniSize = Helpers.UniformScaleUp(IM.RealSize.Width, IM.RealSize.Height, CA.ActualWidth, CA.ActualHeight);
            if (Dispatcher.CheckAccess())
                Transform(400, uniSize, new Point(0d, 0d));
            else
                Dispatcher.Invoke(() => Transform(400, uniSize, new Point(0d, 0d)));
        }

        private void ViewWin_MouseUp(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Right) Close();
        }

        private void Transform(int ms, Size? newSize = null, Point? transPoint = null) {
            if (newSize.HasValue) {
                IM.AnimateDoubleCubicEase(WidthProperty, newSize.Value.Width, ms, EasingMode.EaseOut);
                IM.AnimateDoubleCubicEase(HeightProperty, newSize.Value.Height, ms, EasingMode.EaseOut);
                Scale = newSize.Value.Width / IM.RealSize.Width;
                BM.Show($"{Scale:P1}");
            }
            if (transPoint.HasValue) {
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

        private void SwitchRealSize(int ms = 400, bool? overRide = null) {
            bool toRealSize;
            if (overRide.HasValue) toRealSize = overRide.Value;
            else toRealSize = !IM.IsRealSize;

            var rs = IM.RealSize;
            Size? tgtSize = null;
            Point? tgtPoint = null;
            if (toRealSize)
                tgtSize = rs;
            else {
//                if (IM.ActualHeight <= CA.ActualHeight && IM.ActualWidth <= CA.ActualWidth) return;
                tgtSize = Helpers.UniformScaleUp(rs.Width, rs.Height, CA.ActualWidth, CA.ActualHeight);
                tgtPoint = new Point(0d, 0d);
            }

            //use different animation when image is large to reduce stutter
            if (rs.Width / CA.ActualWidth < 1.8d && rs.Height / CA.ActualHeight < 1.8d)
                Transform(ms, tgtSize, tgtPoint);
            else
                Task.Run(() => {
                    Dispatcher.Invoke(() => IM.AnimateDoubleCubicEase(OpacityProperty, 0.001d, 100, EasingMode.EaseOut));
                    Thread.Sleep(100);
                    Dispatcher.Invoke(() => Transform(0, tgtSize, tgtPoint));
//                    Thread.Sleep(100);
                    Dispatcher.Invoke(() => IM.AnimateDoubleCubicEase(OpacityProperty, 1d, 200, EasingMode.EaseOut), DispatcherPriority.Background);
                });
        }

        /*Point scrollMousePoint;
        double hOff = 1;
        double vOff = 1;
        private void SV_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            scrollMousePoint = e.GetPosition(SV);
            hOff = SV.HorizontalOffset;
            vOff = SV.VerticalOffset;
            SV.CaptureMouse();
        }

        private void SV_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if(SV.IsMouseCaptured)
            {
                SV.ScrollToHorizontalOffset(hOff + 3d * (scrollMousePoint.X - e.GetPosition(SV).X));
                SV.ScrollToVerticalOffset(vOff + 3d * (scrollMousePoint.Y - e.GetPosition(SV).Y));
            }
        }

        private void SV_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            SV.ReleaseMouseCapture();
        }

        private void SV_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e) {
            if (IsActualSize) {
                if (IM.ActualHeight <= SV.ViewportHeight && IM.ActualWidth <= SV.ViewportWidth) return;
                var newSize = Helpers.UniformScaleUp(IM.ActualWidth, IM.ActualHeight, SV.ViewportWidth, SV.ViewportHeight);

                IM.AnimateDoubleCubicEase(WidthProperty, newSize.Width, 400, EasingMode.EaseOut);
                IM.AnimateDoubleCubicEase(HeightProperty, newSize.Height, 400, EasingMode.EaseOut);
                IsActualSize = false;
            }
            else {
                IM.AnimateDoubleCubicEase(WidthProperty, IM.RealSize.Width, 400, EasingMode.EaseOut);
                IM.AnimateDoubleCubicEase(HeightProperty, IM.RealSize.Height, 400, EasingMode.EaseOut);
//                SV.ScrollToHorizontalOffset((SV.ViewportWidth - SV.ExtentWidth) / 2);
//                SV.ScrollToVerticalOffset((SV.ViewportHeight - SV.ExtentHeight) / 2);
//                SV.UpdateLayout();                              
                IsActualSize = true;
            }
            
        }

        private void SV_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            IsActualSize = false;
            var scale = e.Delta > 0 ? 1.2d : 0.8d;

            //prevent zooming out smaller than viewport
            var newSize = Helpers.UniformScaleDown(IM.ActualWidth * scale, IM.ActualHeight * scale,
                SV.ViewportWidth, SV.ViewportHeight);

            IM.AnimateDoubleCubicEase(WidthProperty, newSize.Width, 100, EasingMode.EaseOut);
            IM.AnimateDoubleCubicEase(HeightProperty, newSize.Height, 100, EasingMode.EaseOut);

            e.Handled = true;
//            IM.Width *= scale;
//            IM.Height *= scale;
//
//            var mousePos = e.GetPosition(SV);
//            var scale = e.Delta > 0 ? 0.1d : -0.1d;
////            Console.WriteLine(mousePos.X / SV.ViewportWidth);
////            SV.ScrollToHorizontalOffset(SV.ExtentWidth * mousePos.X / SV.ViewportWidth);
////            SV.ScrollToVerticalOffset(SV.ExtentHeight * mousePos.Y / SV.ViewportHeight);
//            var st = (ScaleTransform)IM.LayoutTransform;
//            st.CenterX = IM.ActualWidth * mousePos.X / SV.ViewportWidth;
//            st.CenterY = IM.ActualHeight * mousePos.Y / SV.ViewportHeight;
//            st.ScaleX += scale;
//            st.ScaleY += scale;

//            var mousePos = e.GetPosition(SV);
//            var scale = e.Delta > 0 ? 0.1d : -0.1d;
//            var st = (ScaleTransform)IM.LayoutTransform;
//            st.ScaleX += scale;
//            st.ScaleY += scale;
        }*/
        private Point mouseCapturePoint;
//        private Point currentPosition;
        private Matrix existingTranslate;


        private void CA_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 1) {
                mouseCapturePoint = e.GetPosition(CA);
                existingTranslate = IM_TT.Value;
//                currentPosition = new Point(Canvas.GetLeft(IM), Canvas.GetTop(IM));
                CA.CaptureMouse();
            }
            else if (e.ClickCount == 2) {
                SwitchRealSize();
            }
        }

        private void CA_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (CA.IsMouseCaptured)
            {
                //Console.WriteLine(mouseCapturePoint.X - e.GetPosition(CA).X);
//                var newX = currentPosition.X + (e.GetPosition(CA).X - mouseCapturePoint.X) * Scale * 2d;
//                if (newX > -currentPosition.X) newX = -currentPosition.X;
//                newX = Math.Round(newX);
//                Canvas.SetLeft(IM, newX);

//                var newY = currentPosition.Y + (e.GetPosition(CA).Y - mouseCapturePoint.Y) * Scale * 2d;
//                if (newY > -currentPosition.Y) newY = -currentPosition.Y;
//                newY = Math.Round(newY);
//                Canvas.SetTop(IM, newY);

//                blury after render transform might be size and zooming?
                IM_TT.BeginAnimation(TranslateTransform.XProperty, null);
                IM_TT.X = existingTranslate.OffsetX + (e.GetPosition(CA).X - mouseCapturePoint.X) * Scale * 2d;
                IM_TT.BeginAnimation(TranslateTransform.YProperty, null);
                IM_TT.Y = existingTranslate.OffsetY + (e.GetPosition(CA).Y- mouseCapturePoint.Y) * Scale * 2d;
#if DEBUG
//                Console.WriteLine(new Point(newX, newY));
#endif
            }
        }

        private void CA_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            CA.ReleaseMouseCapture();
        }


        private void CA_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            if (Transforming) return;
            var scale = e.Delta > 0 ? 1.2d : 1d / 1.2d;
            scaleImage(scale, 50);
        }

        private void scaleImage(double scale, int ms) {
            //prevent zooming out smaller than viewport
            var maxSize = new Size(Math.Min(IM.RealSize.Width, CA.ActualWidth), Math.Min(IM.RealSize.Height, CA.ActualHeight));
            var newSize = Helpers.UniformScaleDown(IM.ActualWidth * scale, IM.ActualHeight * scale, maxSize.Width, maxSize.Height);
            Transform(ms, newSize);
        }

        private void CA_PreviewKeyUp(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Left:
                case Key.Right:
                case Key.Space:
                    var ii = ImageInfo;
                    var il = App.MainWin.ImageList;
                    var i = il.IndexOf(il[ii.ImageRealPath]) + (e.Key == Key.Left ? -1 : 1);
                    if (i > -1 && i < il.Count) {
                        //fade animation
                        IM.AnimateDoubleCubicEase(OpacityProperty, 0d, 200, EasingMode.EaseOut);

                        //load next or previous image
                        Task.Run(() => {
                            var next = il[i];
                            Action<ImageInfo> cb = nii => Dispatcher.Invoke(() => ImageInfo = nii);
                            App.MainWin.LoadFile(next.FilePath, next.Flags, new LoadOptions(fileNames: new[] {next.FileName}, callback: cb));
                            Dispatcher.Invoke(() => {
                                IM.AnimateDoubleCubicEase(OpacityProperty, 1d, 500, EasingMode.EaseOut);
                                ScaleToCanvas();
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
