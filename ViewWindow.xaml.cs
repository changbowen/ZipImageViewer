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
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using FontAwesome.WPF;

namespace ZipImageViewer
{
    public partial class ViewWindow : Window
    {
        public object ImagePath
        {
            get { return GetValue(ImagePathProperty); }
            set { SetValue(ImagePathProperty, value); }
        }
        public static readonly DependencyProperty ImagePathProperty =
            DependencyProperty.Register("ImagePath", typeof(object), typeof(ViewWindow),
                new PropertyMetadata(ImageAwesome.CreateImageSource(FontAwesomeIcon.Image, Brushes.GhostWhite)));


        public ViewWindow()
        {
            InitializeComponent();
//            MaxHeight = SystemParameters.WorkArea.Height;
            Top = 0d;
        }

        private void ViewWin_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void ViewWin_MouseUp(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Right) Close();
        }

        private void ViewWin_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (e.Delta > 0) {
            }
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

    }
}
