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
        /// The value to set if you want to avoid DPI scaling.
        /// </summary>
        public Size RealSize { get; set; }

        public bool IsRealSize => RealSize.Width.Equals(ActualWidth) && RealSize.Height.Equals(ActualHeight);


        protected override Size MeasureOverride(Size constraint) {
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
            //trial 3
            var source = Source as BitmapSource;
            var target = PresentationSource.FromVisual(this)?.CompositionTarget;
            if (source == null || target == null)
                return base.MeasureOverride(constraint);

            var desiredSize = new Size(source.PixelWidth, source.PixelHeight);
            var matrix = target.TransformFromDevice;
            RealSize = new Size(Math.Round(desiredSize.Width * matrix.M11, 3), Math.Round(desiredSize.Height * matrix.M22, 3));
            
            Size realSize;
            if (Stretch == Stretch.Uniform)
                realSize = Helpers.UniformScaleUp(RealSize.Width, RealSize.Height, constraint.Width, constraint.Height);
            else
                realSize = RealSize;

//            if (UseLayoutRounding) {
//                realSize.Width = Math.Round(realSize.Width);
//                realSize.Height = Math.Round(realSize.Height);
//            }

            return realSize;
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

        protected override Size ArrangeOverride(Size finalSize)
        {
            return new Size(DesiredSize.Width, DesiredSize.Height);
//            return new Size(Math.Round(DesiredSize.Width), Math.Round(DesiredSize.Height));
        }
    }
}
