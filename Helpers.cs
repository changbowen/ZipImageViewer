using System;
using System.Windows;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace ZipImageViewer
{
    public static class ExtentionMethods {
        public static void AnimateDoubleCubicEase(this UIElement target, DependencyProperty propdp, double toVal, int ms, EasingMode ease) {
            var anim = new DoubleAnimation(toVal, new Duration(TimeSpan.FromMilliseconds(ms)))
                {EasingFunction = new CubicEase {EasingMode = ease}};
            target.BeginAnimation(propdp, anim);
        }
    }
    public class DpiDecorator : Decorator
    {
        public DpiDecorator()
        {
            Loaded += (s, e) =>
            {
                var m = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice;
                if (!m.HasValue) return;
                var dpiTransform = new ScaleTransform(1 / m.Value.M11, 1 / m.Value.M22);
                if (dpiTransform.CanFreeze) dpiTransform.Freeze();
                LayoutTransform = dpiTransform;
            };
        }

//        protected override Size MeasureOverride(Size constraint) {
//            var b = base.MeasureOverride(constraint);
//            var c = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
//                .Transform(new Vector(b.Width, b.Height));
//            return c.HasValue ? new Size(Math.Round(c.Value.X), Math.Round(c.Value.Y)) : b;
//        }
    }

    public class ImageInfo {
        /// <summary>
        /// For archives, relative path of the file inside the archive. Otherwise name of the file.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// For archives, full path to the archive file. Otherwise full path to the image file.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Indicates the file is an image or belong to an archive.
        /// </summary>
        public App.FileType FileType { get; set; }

        public ImageSource ImageSource { get; set; }

        public string ImageRealPath => FileType == App.FileType.Archive ? FilePath + @"\" + FileName : FilePath;
    }

    public class Helpers
    {
        /// <summary>
        /// Get file type based on extension.
        /// </summary>
        /// <param name="fileName">A full or not full path of the file.</param>
        /// <returns></returns>
        public static App.FileType GetFileType(string fileName) {
            var ft = App.FileType.Unknown;
            var extension = Path.GetExtension(fileName)?.TrimStart('.').ToLowerInvariant();
            if (extension?.Length == 0) return ft;

            if (App.ImageExtensions.Contains(extension)) ft = App.FileType.Image;
            else if (App.ZipExtensions.Contains(extension)) ft = App.FileType.Archive;
            return ft;
        }

        public static BitmapSource GetImageSource(string path, int decodeWidth = 0) {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read)) {
                return GetImageSource(fs, decodeWidth);
            }
        }

        public static BitmapSource GetImageSource(Stream stream, int decodeWidth = 0) {
            stream.Position = 0;
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);

            ushort orien = 0;
            if ((frame.Metadata as BitmapMetadata)?.GetQuery("/app1/ifd/{ushort=274}") is ushort u)
                orien = u;
            frame = null;
//            var size = new Size(frame.PixelWidth, frame.PixelHeight);

            stream.Position = 0;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.DecodePixelWidth = decodeWidth;
            bi.StreamSource = stream;
            bi.EndInit();
            bi.Freeze();

            if (orien < 2) return bi;

            var tb = new TransformedBitmap();
            tb.BeginInit();
            tb.Source = bi;
            switch (orien) {
//              case 1:
//                  break;
                case 2:
                    tb.Transform = new ScaleTransform(-1d, 1d);
                    break;
                case 3:
                    tb.Transform = new RotateTransform(180d);
                    break;
                case 4:
                    tb.Transform = new ScaleTransform(1d, -1d);
                    break;
                case 5: {
                    var tg = new TransformGroup();
                    tg.Children.Add(new RotateTransform(90d));
                    tg.Children.Add(new ScaleTransform(-1d, 1d));
                    tb.Transform = tg;
                    break;
                }
                case 6:
                    tb.Transform = new RotateTransform(90d);
                    break;
                case 7:
                {
                    var tg = new TransformGroup();
                    tg.Children.Add(new RotateTransform(90d));
                    tg.Children.Add(new ScaleTransform(1d, -1d));
                    tb.Transform = tg;
                    break;
                }
                case 8:
                    tb.Transform = new RotateTransform(270d);
                    break;
            }
            tb.EndInit();
            tb.Freeze();
            return tb;
        }

        public static Size UniformScaleUp(double oldW, double oldH, double maxW, double maxH) {
            var ratio = oldW / oldH;
            if (oldW > maxW) {
                oldW = maxW;
                oldH = oldW / ratio;
            }
            if (oldH > maxH) {
                oldH = maxH;
                oldW = oldH * ratio;
            }

            return new Size(oldW, oldH);
        }

        public static Size UniformScaleDown(double oldW, double oldH, double minW, double minH) {
            if (oldW < minW && oldH < minH) {
                var oldRatio = oldW / oldH;
                var minRatio = minW / minH;
                if (oldRatio > minRatio) {
                    oldW = minW;
                    oldH = oldW / oldRatio;
                }
                else {
                    oldH = minH;
                    oldW = oldH * oldRatio;
                }
            }

            return new Size(oldW, oldH);
        } 
    }
}
