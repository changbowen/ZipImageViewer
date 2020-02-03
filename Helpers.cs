using System;
using System.Collections.Generic;
using System.Windows;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.WindowsAPICodePack.Dialogs;
using SizeInt = System.Drawing.Size;

namespace ZipImageViewer
{
    /*public class MatrixAnimation : MatrixAnimationBase
    {
        public Matrix? From
        {
            set { SetValue(FromProperty, value); }
            get { return (Matrix)GetValue(FromProperty); }
        }

        public static DependencyProperty FromProperty =
            DependencyProperty.Register("From", typeof(Matrix?), typeof(MatrixAnimation),
                new PropertyMetadata(null));

        public Matrix? To
        {
            set { SetValue(ToProperty, value); }
            get { return (Matrix)GetValue(ToProperty); }
        }

        public static DependencyProperty ToProperty =
            DependencyProperty.Register("To", typeof(Matrix?), typeof(MatrixAnimation),
                new PropertyMetadata(null));

        public IEasingFunction EasingFunction
        {
            get { return (IEasingFunction)GetValue(EasingFunctionProperty); }
            set { SetValue(EasingFunctionProperty, value); }
        }

        public static readonly DependencyProperty EasingFunctionProperty =
            DependencyProperty.Register("EasingFunction", typeof(IEasingFunction), typeof(MatrixAnimation),
                new UIPropertyMetadata(null));

        public MatrixAnimation()
        {
        }

        public MatrixAnimation(Matrix toValue, Duration duration)
        {
            To = toValue;
            Duration = duration;
        }

        public MatrixAnimation(Matrix toValue, Duration duration, FillBehavior fillBehavior)
        {
            To = toValue;
            Duration = duration;
            FillBehavior = fillBehavior;
        }

        public MatrixAnimation(Matrix fromValue, Matrix toValue, Duration duration)
        {
            From = fromValue;
            To = toValue;
            Duration = duration;
        }

        public MatrixAnimation(Matrix fromValue, Matrix toValue, Duration duration, FillBehavior fillBehavior)
        {
            From = fromValue;
            To = toValue;
            Duration = duration;
            FillBehavior = fillBehavior;
        }

        protected override Freezable CreateInstanceCore()
        {
            return new MatrixAnimation();
        }

        protected override Matrix GetCurrentValueCore(Matrix defaultOriginValue, Matrix defaultDestinationValue, AnimationClock animationClock)
        {
            if (animationClock.CurrentProgress == null)
            {
                return Matrix.Identity;
            }

            var normalizedTime = animationClock.CurrentProgress.Value;
            if (EasingFunction != null)
            {
                normalizedTime = EasingFunction.Ease(normalizedTime);
            }

            var from = From ?? defaultOriginValue;
            var to = To ?? defaultDestinationValue;

            var newMatrix = new Matrix(
                    ((to.M11 - from.M11) * normalizedTime) + from.M11,
                    ((to.M12 - from.M12) * normalizedTime) + from.M12,
                    ((to.M21 - from.M21) * normalizedTime) + from.M21,
                    ((to.M22 - from.M22) * normalizedTime) + from.M22,
                    ((to.OffsetX - from.OffsetX) * normalizedTime) + from.OffsetX,
                    ((to.OffsetY - from.OffsetY) * normalizedTime) + from.OffsetY);

            return newMatrix;
        }
    }*/



    public static class ExtentionMethods
    {
        public static void AnimateDoubleCubicEase(this UIElement target, DependencyProperty propdp, double toVal, int ms, EasingMode ease,
            HandoffBehavior handOff = HandoffBehavior.Compose)
        {
            var anim = new DoubleAnimation(toVal, new Duration(TimeSpan.FromMilliseconds(ms))) { EasingFunction = new CubicEase { EasingMode = ease } };
            target.BeginAnimation(propdp, anim, handOff);
        }
        public static void AnimateDoubleCubicEase(this Animatable target, DependencyProperty propdp, double toVal, int ms, EasingMode ease,
            HandoffBehavior handOff = HandoffBehavior.Compose)
        {
            var anim = new DoubleAnimation(toVal, new Duration(TimeSpan.FromMilliseconds(ms))) { EasingFunction = new CubicEase { EasingMode = ease } };
            target.BeginAnimation(propdp, anim, handOff);
        }


        public static void AnimateBool(this UIElement target, DependencyProperty propdp, bool fromVal, bool toVal, int ms,
            HandoffBehavior handOff = HandoffBehavior.Compose) {
            var anim = new BooleanAnimationUsingKeyFrames();
            if (ms > 0) anim.KeyFrames.Add(new DiscreteBooleanKeyFrame(fromVal, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new DiscreteBooleanKeyFrame(toVal, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms))));
            target.BeginAnimation(propdp, anim, handOff);
        }
        public static void AnimateBool(this Animatable target, DependencyProperty propdp, bool fromVal, bool toVal, int ms,
            HandoffBehavior handOff = HandoffBehavior.Compose) {
            var anim = new BooleanAnimationUsingKeyFrames();
            if (ms > 0) anim.KeyFrames.Add(new DiscreteBooleanKeyFrame(fromVal, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new DiscreteBooleanKeyFrame(toVal, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms))));
            target.BeginAnimation(propdp, anim, handOff);
        }


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
                case 5:
                    {
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

        /// <summary>
        /// Same behavior as Image.Stretch = Uniform. Useful for custom Measure and Arrange passes, as well as scaling on Canvas.
        /// </summary>
        public static Size UniformScale(Size original, Size target) {
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

        public static string OpenFolderDialog(Window owner)
        {
            var cofd = new CommonOpenFileDialog() {
                IsFolderPicker = true
            };

            return cofd.ShowDialog(owner) == CommonFileDialogResult.Ok ? cofd.FileName : null;
        }

    }
}
