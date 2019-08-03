using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZipImageViewer
{
    public class DpiImage : Image
    {
        protected override Size MeasureOverride(Size availableSize) {
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
//            var bitmapImage = Source as BitmapImage;
//            var desiredSize = bitmapImage == null 
//                ? base.MeasureOverride(availableSize) 
//                : new Size(bitmapImage.PixelWidth, bitmapImage.PixelHeight);
//
//            var dpiScale = MiscUtil.GetDpiScale(this);
//            desiredSize = new Size(desiredSize.Width / dpiScale.Width, desiredSize.Height / dpiScale.Height);
//            desiredSize = ImageUtilities.ConstrainWithoutDistorting(desiredSize, availableSize);
//
//            if (UseLayoutRounding) {
//                desiredSize.Width = Math.Round(desiredSize.Width);
//                desiredSize.Height= Math.Round(desiredSize.Height);
//            }
//            return desiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            return new Size(Math.Round(DesiredSize.Width), Math.Round(DesiredSize.Height));
        }
    }
}
