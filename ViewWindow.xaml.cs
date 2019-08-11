using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    public partial class ViewWindow : Window
    {
        public ImageInfo ImageInfo
        {
            get { return (ImageInfo)GetValue(ImageInfoProperty); }
            set { SetValue(ImageInfoProperty, value); }
        }
        public static readonly DependencyProperty ImageInfoProperty =
            Thumbnail.ImageInfoProperty.AddOwner(typeof(ViewWindow));

        private bool IsActualSize = true;

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

            SV_PreviewMouseDoubleClick(null, null);
        }

        private void ViewWin_MouseUp(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Right) Close();
        }

        Point scrollMousePoint;
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
        }

       
    }
}
