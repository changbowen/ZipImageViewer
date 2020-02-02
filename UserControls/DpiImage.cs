using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZipImageViewer
{
    public class DpiImage : Image
    {
        /// <summary>
        /// The device-dependent size to use in WPF to avoid DPI scaling. Updated in Measure pass.
        /// </summary>
        public Size RealSize { get; set; }

        public bool IsRealSize => RealSize.Width.Equals(ActualWidth) && RealSize.Height.Equals(ActualHeight);

        /// <summary>
        /// Caches the last CompositionTarget.TransformFromDevice value.
        /// </summary>
        public Matrix TransformFromDevice { get; set; }

        private void UpdateRealSize() {
            var parentWindow = Window.GetWindow(this);
            var target = parentWindow == null ? PresentationSource.FromVisual(this)?.CompositionTarget :
                                                PresentationSource.FromVisual(parentWindow)?.CompositionTarget;
            Size size = default;
            if (Source is BitmapImage sb)
                size = new Size(sb.PixelWidth, sb.PixelHeight);
            else if (Source is ImageSource si) //to handle when Source is not a BitmapImage
                size = new Size(si.Width, si.Height);
            if (size == default || target == null) return;
            
            TransformFromDevice = target.TransformFromDevice;
            RealSize = new Size(Math.Round(size.Width * TransformFromDevice.M11, 3),
                                Math.Round(size.Height * TransformFromDevice.M22, 3));
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
            if (e.Property == SourceProperty) UpdateRealSize();
            base.OnPropertyChanged(e);
        }


        protected override Size MeasureOverride(Size constraint) {
            //trial 3 uses the helper
            return MeasureArrangeHelper(constraint);
/*
            //trial 1
            Size measureSize = new Size();

            if(Source is BitmapSource bitmapSource)
            {
                var ps = PresentationSource.FromVisual(this);
                if (ps?.CompositionTarget != null)
                {
                    Matrix fromDevice = ps.CompositionTarget.TransformFromDevice;

                    Vector pixelSize = new Vector(bitmapSource.PixelWidth, bitmapSource.PixelHeight);
                    Vector measureSizeV = fromDevice.Transform(pixelSize);
                    measureSize = new Size(measureSizeV.X, measureSizeV.Y);
                    
                }
            }
            return measureSize;
*/
/*
            //trial 2 (not working)
            var baseValue = base.MeasureOverride(constraint);
            var newValue = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
                .Transform(new Vector(baseValue.Width, baseValue.Height));
            return newValue.HasValue ? new Size(newValue.Value.X, newValue.Value.Y) : baseValue;
*/
/*
            //trial 4
            var bitmapImage = Source as BitmapImage;
            var desiredSize = bitmapImage == null 
                ? base.MeasureOverride(constraint) 
                : new Size(bitmapImage.PixelWidth, bitmapImage.PixelHeight);

            var dpiScale = MiscUtil.GetDpiScale(this);
            desiredSize = new Size(desiredSize.Width / dpiScale.Width, desiredSize.Height / dpiScale.Height);
            desiredSize = ImageUtilities.ConstrainWithoutDistorting(desiredSize, constraint);

            if (UseLayoutRounding) {
                desiredSize.Width = Math.Round(desiredSize.Width);
                desiredSize.Height= Math.Round(desiredSize.Height);
            }
            return desiredSize;
*/
        }

        protected override Size ArrangeOverride(Size finalSize) {
            //return base.ArrangeOverride(finalSize);
            //return new Size(DesiredSize.Width, DesiredSize.Height);
            //return new Size(Math.Round(DesiredSize.Width), Math.Round(DesiredSize.Height));
            return MeasureArrangeHelper(finalSize);
        }

        private Size MeasureArrangeHelper(Size constraint) {
            if (RealSize == default) UpdateRealSize();

            Size size = default;
            switch (Stretch) {
                case Stretch.Fill:
                    size.Width = double.IsInfinity(constraint.Width) ? RealSize.Width : constraint.Width;
                    size.Height = double.IsInfinity(constraint.Height) ? RealSize.Height : constraint.Height;
                    break;
                case Stretch.Uniform:
                    size = Helpers.UniformScale(RealSize, constraint);
                    break;
                case Stretch.UniformToFill:
                    size = Helpers.UniformScaleToFill(RealSize, constraint);
                    break;
                case Stretch.None:
                default:
                    size = RealSize;
                    break;
            }
            return size;
        }
    }
}
