using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZipImageViewer
{
    class Helpers
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
    }
}
