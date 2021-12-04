using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZipImageViewer
{
    /// <summary>
    /// DpiImage derives from Image and is not affected by DPI-scaled UI and renders 1-to-1 on the display.
    /// Size is converted back based on the UI scale of the parent window or the image itself.
    /// Be sure to configure Stretch and StretchDirection correctly for the 1-to-1 size to work.
    /// </summary>
    public class DpiImage : Image
    {
        /// <summary>
        /// The device-dependent size to use in WPF to avoid DPI scaling. Updated in Measure pass.
        /// This is the "WPF" size if you will.
        /// </summary>
        public Size RealSize { get; set; }

        public bool IsRealSize => RealSize.Width.Equals(Math.Round(ActualWidth, 3)) && RealSize.Height.Equals(Math.Round(ActualHeight, 3));

        public double Scale => Width / RealSize.Width;


        /// <summary>
        /// Indicate whether animations are being played on the image.
        /// </summary>
        public bool Transforming {
            get { return (bool)GetValue(TransformingProperty); }
            set { SetValue(TransformingProperty, value); }
        }
        public static readonly DependencyProperty TransformingProperty =
            DependencyProperty.Register("Transforming", typeof(bool), typeof(DpiImage), new PropertyMetadata(false));


        /// <summary>
        /// Same as the last CompositionTarget.TransformFromDevice value.
        /// </summary>
        public Point DpiMultiplier { get; set; }

        private void UpdateRealSize() {
            Size size = default;
            if (Source is BitmapSource sb)
                size = new Size(sb.PixelWidth, sb.PixelHeight);
            else if (Source is ImageSource si) //to handle when Source is not a BitmapImage
                size = new Size(si.Width, si.Height);
            if (size == default) return;

            //old .Net implementation (v4.6.1)
            //var parentWindow = Window.GetWindow(this);
            //var target = parentWindow == null ? PresentationSource.FromVisual(this)?.CompositionTarget :
            //PresentationSource.FromVisual(parentWindow)?.CompositionTarget;
            //DpiMultiplier = target.TransformFromDevice;
            //RealSize = new Size(Math.Round(size.Width * DpiMultiplier.M11, 3),
            //                    Math.Round(size.Height * DpiMultiplier.M22, 3));

            //new .Net implementation (v4.6.2+)
            var dpiScale = VisualTreeHelper.GetDpi(this);
            DpiMultiplier = new Point(1d / dpiScale.DpiScaleX, 1d / dpiScale.DpiScaleY);
            RealSize = new Size(Math.Round(size.Width * DpiMultiplier.X, 3), Math.Round(size.Height * DpiMultiplier.Y, 3));

//#if DEBUG
//            Console.WriteLine($"{nameof(RealSize)}: {RealSize}; Scale: {DpiMultiplier.X};");
//#endif
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) {
            UpdateRealSize();
            base.OnDpiChanged(oldDpi, newDpi);
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
            if (e.Property == SourceProperty) {
                if (e.NewValue == null)
                    RealSize = new Size();
                else
                    UpdateRealSize();
            }
            base.OnPropertyChanged(e);
        }

        protected override Size MeasureOverride(Size constraint) {
            if (RealSize == default) UpdateRealSize();
            return MeasureArrangeHelper(constraint);
        }

        protected override Size ArrangeOverride(Size finalSize) {
            return MeasureArrangeHelper(finalSize);
        }

        private Size MeasureArrangeHelper(Size constraint) {
            if (Source == null) return new Size();
            
            //get computed scale factor (source from Reference Source)
            Size scaleFactor = Helpers.ComputeScaleFactor(constraint, RealSize, Stretch, StretchDirection);

            // Returns our minimum size & sets DesiredSize.
            return new Size(RealSize.Width * scaleFactor.Width, RealSize.Height * scaleFactor.Height);

            //old implementation without support for StretchDirection
            //Size size = default;
            //switch (Stretch) {
            //    case Stretch.Fill:
            //        size.Width = double.IsInfinity(constraint.Width) ? RealSize.Width : constraint.Width;
            //        size.Height = double.IsInfinity(constraint.Height) ? RealSize.Height : constraint.Height;
            //        break;
            //    case Stretch.Uniform:
            //        size = Helpers.UniformScale(RealSize, constraint);
            //        break;
            //    case Stretch.UniformToFill:
            //        size = Helpers.UniformScaleToFill(RealSize, constraint);
            //        break;
            //    case Stretch.None:
            //    default:
            //        size = RealSize;
            //        break;
            //}
            //return size;
        }
    }
}
