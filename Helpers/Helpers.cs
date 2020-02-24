﻿using System;
using System.Windows;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.WindowsAPICodePack.Dialogs;
using SizeInt = System.Drawing.Size;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

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
            if (multiplesOf == 0d) return 0d;
            return Math.Ceiling(input / multiplesOf) * multiplesOf;
        }

        //public static double Round3(this double input) {
        //    return Math.Round(input, 3, MidpointRounding.ToEven);
        //}

        public static SizeInt DivideBy(this SizeInt input, double d) {
            return new SizeInt((int)Math.Round(input.Width / d), (int)Math.Round(input.Height / d));
        }
    }

    /// <summary>
    /// FileFlags also determines the sorting order (defined in FolderSorter).
    /// Being casted to int, lower number comes in the front. Except for Unknown which comes last;
    /// </summary>
    [Flags]
    public enum FileFlags {
        Unknown = 0,
        Error = 1,
        Directory = 2,
        Archive = 4,
        Image = 8,
        ///// <summary>
        ///// Indicate to load all archive content instead of a single image
        ///// </summary>
        //Archive_OpenSelf = 8
    }

    /// <summary>
    /// Sort based on int value of FileFlag.
    /// For same flags (same type of file), do string compare on VirtualPath.
    /// </summary>
    public class FolderSorter : IComparer
    {
        public int Compare(object x, object y) {
            var oX = (ObjectInfo)x;
            var oY = (ObjectInfo)y;

            //strip supporting bits
            var baseX = oX.Flags & ~FileFlags.Error;
            var baseY = oY.Flags & ~FileFlags.Error;

            if (baseX != baseY) {
                if (baseX == FileFlags.Unknown) return 1; //put unknowns in last
                if (baseY == FileFlags.Unknown) return -1; //put unknowns in last
                return baseX - baseY;
            }
            else {
                return string.Compare(oX.VirtualPath, oY.VirtualPath);
            }
        }
    }

    public static class Helpers {
        /// <summary>
        /// Get file type based on extension. Assumes fileName points to a file.
        /// </summary>
        /// <param name="fileName">A full or not full path of the file.</param>
        /// <returns></returns>
        public static FileFlags GetPathType(string fileName) {
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

        public static string OpenFolderDialog(MainWindow win, Action<string> callback = null)
        {
            var cofd = new CommonOpenFileDialog() { IsFolderPicker = true };

            if (cofd.ShowDialog(win) == CommonFileDialogResult.Ok && !string.IsNullOrEmpty(cofd.FileName)) {
                var path = cofd.FileName;
                if (callback != null) callback.Invoke(path);
                return path;
            }
            return null;
        }

        public static void Run(string path, string args) {
            var info = new ProcessStartInfo(path, args) {
                WorkingDirectory = App.ExeDir
            };
            Process.Start(path, args);
        }

        public static string BytesToString(long byteCount) {
            string[] suf = { " B", " KB", " MB", " GB", " TB", " PB", " EB" }; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

        public static string CustomCmdArgsReplace(string input, ObjectInfo objInfo) {
            return input.Replace(@"%FileSystemPath%", objInfo.FileSystemPath);
        }


        public static T GetVisualChild<T>(DependencyObject parent) where T : Visual {
            T child = default;

            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++) {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null) {
                    child = GetVisualChild<T>(v);
                }
                if (child != null) {
                    break;
                }
            }
            return child;
        }

        public static bool IsFullScreen(Window win, Rect monitorInfo = default) {
            if (monitorInfo == default)
                monitorInfo = NativeHelpers.GetMonitorFromWindow(win);

            return win.Top == monitorInfo.Top && win.Left == monitorInfo.Left && win.Width == monitorInfo.Width && win.Height == monitorInfo.Height;
        }

        public static void SwitchFullScreen(Window win, ref Rect lastRect, bool? fullScreen = null, Rect monitorInfo = default) {
            if (monitorInfo == default)
                monitorInfo = NativeHelpers.GetMonitorFromWindow(win);

            if (fullScreen == null)
                fullScreen = !IsFullScreen(win, monitorInfo);

            win.WindowState = WindowState.Normal;//need WindowState.Normal to hide taskbar under fullscreen
            if (fullScreen.Value) {
                //go fullscreen
                lastRect = new Rect(win.Left, win.Top, win.Width, win.Height);
                win.Top = monitorInfo.Top;
                win.Left = monitorInfo.Left;
                win.Width = monitorInfo.Width;
                win.Height = monitorInfo.Height;
            }
            else {
                //restore last
                win.Top = lastRect.Top;
                win.Left = lastRect.Left;
                win.Width = lastRect.Width;
                win.Height = lastRect.Height;
            }
        }
    }


    public class VirtualizingWrapPanel : WpfToolkit.Controls.VirtualizingWrapPanel
    {
        public int TotalRowCount => rowCount;
        public int RowItemCount => itemsPerRowCount;
        public new int VisualChildrenCount => base.VisualChildrenCount;
    }
}
