using System;
using System.Windows;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.WindowsAPICodePack.Dialogs;
using SizeInt = System.Drawing.Size;
using System.Windows.Controls;

namespace ZipImageViewer
{
    public static class ExtentionMethods
    {
        public static void AnimateDoubleCubicEase(this UIElement target, DependencyProperty propdp, double toVal, int ms, EasingMode ease,
            HandoffBehavior handOff = HandoffBehavior.Compose, int begin = 0, EventHandler completed = null)
        {
            var anim = new DoubleAnimation(toVal, new Duration(TimeSpan.FromMilliseconds(ms)));
            switch (ease) {
                case EasingMode.EaseIn:
                    anim.EasingFunction = App.CE_EaseIn;
                    break;
                case EasingMode.EaseOut:
                    anim.EasingFunction = App.CE_EaseOut;
                    break;
                case EasingMode.EaseInOut:
                    anim.EasingFunction = App.CE_EaseInOut;
                    break;
            }
            if (begin > 0) anim.BeginTime = TimeSpan.FromMilliseconds(begin);
            if (completed != null) anim.Completed += completed;
            target.BeginAnimation(propdp, anim, handOff);
        }
        //public static void AnimateDoubleCubicEase(this Animatable target, DependencyProperty propdp, double toVal, int ms, EasingMode ease,
        //    HandoffBehavior handOff = HandoffBehavior.Compose, int begin = 0)
        //{
        //    var anim = new DoubleAnimation(toVal, new Duration(TimeSpan.FromMilliseconds(ms))) { EasingFunction = new CubicEase { EasingMode = ease } };
        //    if (begin > 0) anim.BeginTime = TimeSpan.FromMilliseconds(begin);
        //    target.BeginAnimation(propdp, anim, handOff);
        //}


        public static void AnimateBool(this UIElement target, DependencyProperty propdp, bool fromVal, bool toVal, int ms,
            HandoffBehavior handOff = HandoffBehavior.Compose) {
            var anim = new BooleanAnimationUsingKeyFrames();
            if (ms > 0) anim.KeyFrames.Add(new DiscreteBooleanKeyFrame(fromVal, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new DiscreteBooleanKeyFrame(toVal, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms))));
            target.BeginAnimation(propdp, anim, handOff);
        }
        //public static void AnimateBool(this Animatable target, DependencyProperty propdp, bool fromVal, bool toVal, int ms,
        //    HandoffBehavior handOff = HandoffBehavior.Compose) {
        //    var anim = new BooleanAnimationUsingKeyFrames();
        //    if (ms > 0) anim.KeyFrames.Add(new DiscreteBooleanKeyFrame(fromVal, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        //    anim.KeyFrames.Add(new DiscreteBooleanKeyFrame(toVal, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms))));
        //    target.BeginAnimation(propdp, anim, handOff);
        //}


        public static double RoundToMultiplesOf(this double input, double multiplesOf) {
            return Math.Ceiling(input / multiplesOf) * multiplesOf;
        }

        //public static double Round3(this double input) {
        //    return Math.Round(input, 3, MidpointRounding.ToEven);
        //}

        public static SizeInt DivideBy(this SizeInt input, double d) {
            return new SizeInt((int)Math.Round(input.Width / d), (int)Math.Round(input.Height / d));
        }
    }

/*    public class DpiDecorator : Decorator
    {
        public DpiDecorator()
        {
            Loaded += OnLoaded;
            Unloaded -= OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (null != e) e.Handled = true;

            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                var matrix = source.CompositionTarget.TransformToDevice;
                var dpiTransform = new ScaleTransform(1 / matrix.M11, 1 / matrix.M22);
                if (dpiTransform.CanFreeze) dpiTransform.Freeze();
                LayoutTransform = dpiTransform;
            }
        }
    }*/

    [Flags]
    public enum FileFlags {
        Unknown = 0,
        Image = 1,
        Archive = 2,
        Directory = 4,
        /// <summary>
        /// Indicate to load all archive content instead of a single image
        /// </summary>
        Archive_OpenSelf = 8
    }


    public class LoadOptions {
        public string FilePath { get; } = null;
        public FileFlags Flags { get; set; } = FileFlags.Unknown;
        public SizeInt DecodeSize { get; set; } = default;
        public string Password { get; set; } = null;
        public string[] FileNames { get; set; } = null;
        /// <summary>
        /// The number of files to extract. Ignored when FileNames are not empty.
        /// </summary>
        public int ExtractCount { get; set; } = 0;
        /// <summary>
        /// Should be called on returning of each ObjectInfo (usually file system objects).
        /// </summary>
        public Action<ObjectInfo> ObjInfoCallback { get; set; } = null;
        /// <summary>
        /// Should be called on returning of each child ObjectInfo (thumbnails, raw images).
        /// </summary>
        public Action<ObjectInfo> CldInfoCallback { get; set; } = null;

        public LoadOptions(string filePath) {
            FilePath = filePath;
        }

        //public LoadOptions(SizeInt decodeSize = default, string password = null,
        //    string[] fileNames = null, int extractCount = 0,
        //    Action<ObjectInfo> callback = null, ObjectInfo objInfo = null) {
        //    DecodeSize = decodeSize;
        //    Password = password;
        //    FileNames = fileNames;
        //    ExtractCount = extractCount;
        //    Callback = callback;
        //}

        //public LoadOptions(string filePath, FileFlags flags, bool isThumb = true, Action<ObjectInfo> callback = null) {
        //    FilePath = filePath;
        //    Flags = flags;
        //    if (isThumb) DecodeSize = Setting.ThumbnailSize;
        //    Callback = callback;
        //}
    }

    public class Helpers
    {
        /// <summary>
        /// Get file type based on extension. Assumes fileName points to a file.
        /// </summary>
        /// <param name="fileName">A full or not full path of the file.</param>
        /// <returns></returns>
        public static FileFlags GetPathType(string fileName)
        {
            var ft = FileFlags.Unknown;
            var extension = Path.GetExtension(fileName)?.TrimStart('.').ToLowerInvariant();
            if (extension?.Length == 0) return ft;

            if (App.ImageExtensions.Contains(extension)) ft = FileFlags.Image;
            else if (App.ZipExtensions.Contains(extension)) ft = FileFlags.Archive;
            return ft;
        }

        public static FileFlags GetPathType(FileSystemInfo fsInfo) {
            if (fsInfo.Attributes.HasFlag(FileAttributes.Directory))
                return FileFlags.Directory;
            if (App.ZipExtensions.Contains(fsInfo.Extension.ToLowerInvariant()))
                return FileFlags.Archive;
            if (App.ImageExtensions.Contains(fsInfo.Extension.ToLowerInvariant()))
                return FileFlags.Image;
            return FileFlags.Unknown;
        }

        public static BitmapSource GetImageSource(string path, SizeInt decodeSize)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return GetImageSource(fs, decodeSize);
            }
        }

        public static BitmapSource GetImageSource(Stream stream, SizeInt decodeSize)
        {
            stream.Position = 0;
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            var frameSize = new Size(frame.PixelWidth, frame.PixelHeight);
            ushort orien = 0;
            if ((frame.Metadata as BitmapMetadata)?.GetQuery("/app1/ifd/{ushort=274}") is ushort u)
                orien = u;
            frame = null;

            //flip decodeSize according to orientation
            if (decodeSize.Width + decodeSize.Height > 0 && orien > 4 && orien < 9)
                decodeSize = new SizeInt(decodeSize.Height, decodeSize.Width);

            //init bitmapimage
            stream.Position = 0;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            if (frameSize.Width > frameSize.Height)
                bi.DecodePixelHeight = decodeSize.Height;
            else
                bi.DecodePixelWidth = decodeSize.Width;
            bi.StreamSource = stream;
            bi.EndInit();
            bi.Freeze();

            if (orien < 2) return bi;
            //apply orientation based on metadata
            var tb = new TransformedBitmap();
            tb.BeginInit();
            tb.Source = bi;
            switch (orien)
            {
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
                case 7: {
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


        private static double GetAverageBrightness(BitmapFrame frame) {
            using (var bmpStream = new MemoryStream()) {
                var enc = new BmpBitmapEncoder();
                enc.Frames.Add(frame);
                enc.Save(bmpStream);
                using (var bmp = new System.Drawing.Bitmap(bmpStream)) {
                    using (var bmpS = new System.Drawing.Bitmap(bmp, new SizeInt(100, 100))) {
                        double bri = 0d;
                        for (int x = 0; x < bmpS.Width; x++) {
                            for (int y = 0; y < bmpS.Height; y++) {
                                var b = bmpS.GetPixel(x, y).GetBrightness();
                                bri += b;
                            }
                        }
                        return bri / bmpS.Width / bmpS.Height;
                    }
                }
            }
        }

        /// <summary>
        /// Uniform scale origional size to target size. Does not scale up.
        /// </summary>
        public static Size UniformScaleDown(Size original, Size target) {
            if (original.Width == 0d || original.Height == 0d) return original;

            var ratio = original.Width / original.Height;
            if (original.Width > target.Width) {
                original.Width = target.Width;
                original.Height = original.Width / ratio;
            }

            if (original.Height > target.Height) {
                original.Height = target.Height;
                original.Width = original.Height * ratio;
            }
            return original;
        }

        /// <summary>
        /// Same behavior as Image.Stretch = UniformToFill. Useful for custom Measure and Arrange passes, as well as scaling on Canvas.
        /// Does not scale up.
        /// </summary>
        public static Size UniformScaleToFill(Size original, Size target) {
            if (original.Width == 0d || original.Height == 0d) return original;
            
            var ratio = original.Width / original.Height;
            if (ratio > 1d) {//wide image
                if (!double.IsInfinity(target.Height) && original.Height > target.Height) {
                    original.Height = target.Height;
                    original.Width = original.Height * ratio;
                }
            }
            else {//tall image
                if (!double.IsInfinity(target.Width) && original.Width > target.Width) {
                    original.Width = target.Width;
                    original.Height = original.Width / ratio;
                }
            }
            return original;
        }


        /// <summary>
        /// From .Net ViewBox.cs source
        /// </summary>
        internal static Size ComputeScaleFactor(Size availableSize, Size contentSize, Stretch stretch, StretchDirection stretchDirection) {
            // Compute scaling factors to use for axes
            double scaleX = 1.0;
            double scaleY = 1.0;

            bool isConstrainedWidth = !double.IsPositiveInfinity(availableSize.Width);
            bool isConstrainedHeight = !double.IsPositiveInfinity(availableSize.Height);

            if ((stretch == Stretch.Uniform || stretch == Stretch.UniformToFill || stretch == Stretch.Fill) &&
                (isConstrainedWidth || isConstrainedHeight)) {
                // Compute scaling factors for both axes
                scaleX = contentSize.Width == 0d ? 0d : availableSize.Width / contentSize.Width;
                scaleY = contentSize.Height == 0d ? 0d : availableSize.Height / contentSize.Height;

                if (!isConstrainedWidth) scaleX = scaleY;
                else if (!isConstrainedHeight) scaleY = scaleX;
                else {
                    // If not preserving aspect ratio, then just apply transform to fit
                    switch (stretch) {
                        case Stretch.Uniform:       //Find minimum scale that we use for both axes
                            double minscale = scaleX < scaleY ? scaleX : scaleY;
                            scaleX = scaleY = minscale;
                            break;

                        case Stretch.UniformToFill: //Find maximum scale that we use for both axes
                            double maxscale = scaleX > scaleY ? scaleX : scaleY;
                            scaleX = scaleY = maxscale;
                            break;

                        case Stretch.Fill:          //We already computed the fill scale factors above, so just use them
                            break;
                    }
                }

                //Apply stretch direction by bounding scales.
                //In the uniform case, scaleX=scaleY, so this sort of clamping will maintain aspect ratio
                //In the uniform fill case, we have the same result too.
                //In the fill case, note that we change aspect ratio, but that is okay
                switch (stretchDirection) {
                    case StretchDirection.UpOnly:
                        if (scaleX < 1.0) scaleX = 1.0;
                        if (scaleY < 1.0) scaleY = 1.0;
                        break;

                    case StretchDirection.DownOnly:
                        if (scaleX > 1.0) scaleX = 1.0;
                        if (scaleY > 1.0) scaleY = 1.0;
                        break;

                    case StretchDirection.Both:
                        break;

                    default:
                        break;
                }
            }
            //Return this as a size now
            return new Size(scaleX, scaleY);
        }


        public static string OpenFolderDialog(Window owner)
        {
            var cofd = new CommonOpenFileDialog() {
                IsFolderPicker = true
            };

            return cofd.ShowDialog(owner) == CommonFileDialogResult.Ok ? cofd.FileName : null;
        }

    }
}
