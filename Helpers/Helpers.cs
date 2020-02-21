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


    public class LoadOptions {
        public string FilePath { get; } = null;
        public FileFlags Flags { get; set; } = FileFlags.Unknown;
        public SizeInt DecodeSize { get; set; } = default;
        public string Password { get; set; } = null;
        public string[] FileNames { get; set; } = null;
        /// <summary>
        /// Whether to load image content. Set to false to only return file list.
        /// </summary>
        public bool LoadImage { get; set; } = false;
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

        /// <summary>
        /// Only udpate when SourcePaths is null. Call from background thread.
        /// </summary>
        internal static void UpdateSourcePaths(ObjectInfo objInfo) {
            if (objInfo.SourcePaths != null) return;

            switch (objInfo.Flags) {
                case FileFlags.Directory:
                    IEnumerable<FileSystemInfo> fsInfos = null;
                    try { fsInfos = new DirectoryInfo(objInfo.FileSystemPath).EnumerateFileSystemInfos(); }
                    catch { objInfo.Flags |= FileFlags.Error; }
                    var srcPaths = new List<string>();
                    foreach (var fsInfo in fsInfos) {
                        var fType = GetPathType(fsInfo);
                        if (fType != FileFlags.Image) continue;
                        srcPaths.Add(fsInfo.FullName);
                    }
                    objInfo.SourcePaths = srcPaths.ToArray();
                    break;
                case FileFlags.Archive:
                    MainWindow.LoadFile(new LoadOptions(objInfo.FileSystemPath) {
                        Flags = FileFlags.Archive,
                        ObjInfoCallback = oi => objInfo.SourcePaths = oi.SourcePaths,
                    });
                    break;
                case FileFlags.Image:
                    objInfo.SourcePaths = new[] { objInfo.FileSystemPath };
                    break;
                case FileFlags.Archive | FileFlags.Image:
                    //FileFlags.Archive | FileFlags.Image should have SourcePaths[1] set when loaded the first time.
                    //this is only included for completeness and should never be reached unless something's wrong with the code.
                    objInfo.SourcePaths = new[] { objInfo.FileName };
                    break;
                default:
                    objInfo.SourcePaths = new string[0];
                    break;
            }
            //Console.WriteLine("Updated SourcePaths for: " + objInfo.FileSystemPath);
        }


        //private static HashSet<string> loading = new HashSet<string>();
        //private static readonly object lock_Loading = new object();

        /// <summary>
        /// Used to get image from within a container. Flags will contain Error if error occurred.
        /// </summary>
        /// <param name="objInfo">The ObjectInfo of the container.</param>
        /// <param name="sourcePathIdx">Index of the file to load in ObjectInfo.SourcePaths.</param>
        public static async Task<ImageSource> GetImageSource(ObjectInfo objInfo, int sourcePathIdx, bool isThumb) {
            //var targetPath = objInfo.SourcePaths[sourcePathIdx];
            //var fsPath = objInfo.FileSystemPath;
            //var flags = objInfo.Flags;
            var size = isThumb ? (SizeInt)Setting.ThumbnailSize : default;
            //var imgSource = objInfo.ImageSource;
            return await Task.Run(() => GetImageSource(objInfo, sourcePathIdx, size));
        }

        /// <summary>
        /// Used to get image from within a container.
        /// </summary>
        /// <param name="size">Decode size.</param>
        public static ImageSource GetImageSource(ObjectInfo objInfo, int sourcePathIdx, SizeInt size) {
            if (objInfo.Flags.HasFlag(FileFlags.Error)) return App.fa_exclamation;
            if (objInfo.Flags == FileFlags.Unknown) return App.fa_file;

#if DEBUG
            var now = DateTime.Now;
#endif
            App.LoadThrottle.Wait();
#if DEBUG
            Console.WriteLine($"Helpers.GetImageSource() waited {(DateTime.Now - now).TotalMilliseconds}ms. Remaining slots: {App.LoadThrottle.CurrentCount}");
#endif

            ImageSource source = null;
            try {
                //flags is the parent container type
                switch (objInfo.Flags) {
                    case FileFlags.Directory:
                        if (objInfo.SourcePaths?.Length > 0)
                            source = GetImageSource(objInfo.SourcePaths[sourcePathIdx], size);
                        if (source == null) {
                            source = App.fa_folder;
                        }
                        break;
                    case FileFlags.Image:
                        source = GetImageSource(objInfo.FileSystemPath, size);
                        if (source == null) {
                            source = App.fa_image;
                        }
                        break;
                    case FileFlags.Archive:
                        if (objInfo.SourcePaths?.Length > 0) {
                            MainWindow.LoadFile(new LoadOptions(objInfo.FileSystemPath) {
                                DecodeSize = size,
                                LoadImage = true,
                                FileNames = new[] { objInfo.SourcePaths[sourcePathIdx] },
                                Flags = FileFlags.Archive,
                                CldInfoCallback = oi => source = oi.ImageSource,
                                ObjInfoCallback = oi => objInfo.Flags = oi.Flags
                            });
                        }
                        if (source == null) {
                            source = App.fa_archive;
                        }
                        break;
                    case FileFlags.Archive | FileFlags.Image:
                        source = objInfo.ImageSource;
                        if (source == null) {
                            source = App.fa_image;
                        }
                        break;
                }
            }
            finally {
                objInfo = null;

                App.LoadThrottle.Release();
#if DEBUG
                Console.WriteLine($"Helpers.GetImageSource() exited leaving {App.LoadThrottle.CurrentCount} slots.");
#endif
            }
            return source;
        }

        /// <summary>
        /// Load image from disk if cache is not availble.
        /// </summary>
        public static BitmapSource GetImageSource(string path, SizeInt decodeSize = default) {
            BitmapSource bs = null;
            try {
                var isThumb = decodeSize.Width + decodeSize.Height > 0;
                if (isThumb) {
                    //try load from cache when decodeSize is non-zero
                    bs = SQLiteHelper.GetFromThumbDB(path, decodeSize);
                    if (bs != null) return bs;
                }

                ////avoid file dead locks
                //Monitor.Enter(lock_Loading);
                //var wait = loading.Contains(path);
                //Monitor.Exit(lock_Loading);
                //while (wait) {
                //    Thread.Sleep(100);
                //    Monitor.Enter(lock_Loading);
                //    wait = loading.Contains(path);
                //    Monitor.Exit(lock_Loading);
                //}
                //Monitor.Enter(lock_Loading);
                //loading.Add(path);
                //Monitor.Exit(lock_Loading);
                //load from disk
#if DEBUG
                Console.WriteLine("Loading from disk: " + path);
#endif
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read)) {
                    bs = GetImageSource(fs, decodeSize);
                }
                if (isThumb && bs != null) SQLiteHelper.AddToThumbDB(bs, path, decodeSize);
            }
            catch (Exception ex) {
                MessageBox.Show($"Error loading file {path}.\r\n{ex.Message}", "Error Loading File", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally {
                //loading.Remove(path);
            }
            
            return bs;
        }

        /// <summary>
        /// Decode image from stream (FileStream when loading from file or MemoryStream when loading from archive.
        /// </summary>
        public static BitmapSource GetImageSource(Stream stream, SizeInt decodeSize) {
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
            if (frameSize.Width > 0 && frameSize.Height > 0) {
                var frameRatio = frameSize.Width / frameSize.Height;
                if (decodeSize.Width > 0 && decodeSize.Height > 0) {
                    if (frameRatio > (double)decodeSize.Width / decodeSize.Height)
                        bi.DecodePixelHeight = decodeSize.Height;
                    else
                        bi.DecodePixelWidth = decodeSize.Width;
                }
                else if (decodeSize.Width == 0 && decodeSize.Height > 0)
                    bi.DecodePixelHeight = decodeSize.Height;
                else if (decodeSize.Height == 0 && decodeSize.Width > 0)
                    bi.DecodePixelWidth = decodeSize.Width;
            }
            bi.StreamSource = stream;
            bi.EndInit();
            bi.Freeze();

            if (orien < 2) return bi;
            //apply orientation based on metadata
            var tb = new TransformedBitmap();
            tb.BeginInit();
            tb.Source = bi;
            switch (orien) {
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
