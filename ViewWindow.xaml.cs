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

        public ViewWindow()
        {
            InitializeComponent();
        }

        private void ViewWindow_Loaded(object sender, RoutedEventArgs e) {
//            var rect = NativeMethods.GetMonitorFromWindow(this);
//            Top = rect.Top;
//            Left = rect.Left;
//            Width = rect.Width;
//            Height = rect.Height;
            if (double.IsNaN(IM.Width)) IM.Width = IM.ActualWidth;
            if (double.IsNaN(IM.Height)) IM.Height = IM.ActualHeight;

            SwitchRealSize();
        }

        private void ViewWin_MouseUp(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Right) Close();
        }

        private void Transform(int ms, Size? newSize = null, Point? transPoint = null) {
            if (newSize.HasValue) {
                IM.AnimateDoubleCubicEase(WidthProperty, newSize.Value.Width, ms, EasingMode.EaseOut);
                IM.AnimateDoubleCubicEase(HeightProperty, newSize.Value.Height, ms, EasingMode.EaseOut);
                Scale = newSize.Value.Width / IM.RealSize.Width;
                BM.Show($"{Scale:P0}");
            }
            if (transPoint.HasValue) {
                IM_TT.AnimateDoubleCubicEase(TranslateTransform.XProperty, transPoint.Value.X, ms, EasingMode.EaseOut);
                IM_TT.AnimateDoubleCubicEase(TranslateTransform.YProperty, transPoint.Value.Y, ms, EasingMode.EaseOut);
            }

            var animBool = new BooleanAnimationUsingKeyFrames();
            animBool.KeyFrames.Add(new DiscreteBooleanKeyFrame(true, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animBool.KeyFrames.Add(new DiscreteBooleanKeyFrame(false, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms + 10))));
            BeginAnimation(TransformingProperty, animBool);
        }

        private void SwitchRealSize() {
            if (IM.IsRealSize) {
                if (IM.ActualHeight <= CA.ActualHeight && IM.ActualWidth <= CA.ActualWidth) return;
                var newSize = Helpers.UniformScaleUp(IM.ActualWidth, IM.ActualHeight, CA.ActualWidth, CA.ActualHeight);
                Transform(400, newSize, new Point(0, 0));
            }
            else Transform(400, IM.RealSize);
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
        private Matrix existingTranslate;

        private void CA_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 1) {
                mouseCapturePoint = e.GetPosition(CA);
                existingTranslate = IM_TT.Value;
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
//                Console.WriteLine(mouseCapturePoint.X - e.GetPosition(CA).X);
                IM_TT.BeginAnimation(TranslateTransform.XProperty, null);
                IM_TT.X = existingTranslate.OffsetX + (e.GetPosition(CA).X - mouseCapturePoint.X) * Scale * 2d;
                IM_TT.BeginAnimation(TranslateTransform.YProperty, null);
                IM_TT.Y = existingTranslate.OffsetY + (e.GetPosition(CA).Y- mouseCapturePoint.Y) * Scale * 2d;
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
    }
}
