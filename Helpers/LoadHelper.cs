using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SizeInt = System.Drawing.Size;
using static ZipImageViewer.Helpers;
using static ZipImageViewer.TableHelper;
using static ZipImageViewer.SQLiteHelper;
using System.Data;
using SevenZip;

namespace ZipImageViewer
{
    public static class LoadHelper
    {
        public class LoadOptions
        {
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
            /// <summary>
            /// <para>SevenZipExtractor's extract methods should only be used with when LoadImage == false to avoid extracting twice.</para>
            /// <para>This is called first for each image in the archive. Returned ImageSource will be used when it's not null and LoadImage == false.</para>
            /// <para>No need to call dispose on the extractor.</para>
            /// </summary>
            public Func<SevenZipExtractor, string, LoadOptions, ImageSource> ExtractorCallback { get; set; } = null;
            /// <summary>
            /// Indicate whether to continue extracting files.
            /// </summary>
            public bool Continue { get; set; } = true;
            public bool TryCache { get; set; } = true;

            public LoadOptions(string filePath) {
                FilePath = filePath;
            }
        }

        public static readonly int MaxLoadThreads = Environment.ProcessorCount;
        public static SemaphoreSlim LoadThrottle = new SemaphoreSlim(MaxLoadThreads);

        /// <summary>
        /// <para>
        /// ObjectInfo.Flags in ObjInfoCallback will contain FileFlag.Error when extraction fails.
        /// ObjectInfo.SourcePaths in ObjInfoCallback contains the file list inside archive.
        /// ObjectInfo in CldInfoCallback contains information for files inside archive.
        /// </para>
        /// Should be called from a background thread.
        /// Callback can be used to manipulate the loaded images. For e.g. display it in the ViewWindow, or add to ObjectList as thumbnails.
        /// Callback is called for each image loaded.
        /// Use Dispatcher if callback needs to access the UI thread.
        /// </summary>
        public static void ExtractZip(LoadOptions options, CancellationTokenSource tknSrc = null) {
            if (tknSrc?.IsCancellationRequested == true) return;

            //objInfo to be returned
            var objInfo = new ObjectInfo(options.FilePath, options.Flags) {
                FileName = Path.GetFileName(options.FilePath)
            };

            //some files may get loaded from cache therefore unaware of whether password is correct
            //the HashSet records processed files through retries
            var done = new HashSet<string>();
            for (int caseIdx = 0; caseIdx < 4; caseIdx++) {
                if (tknSrc?.IsCancellationRequested == true) break;

                var success = tryExtract(caseIdx, options, objInfo, done, tknSrc);
                if (success) break;
            }

            if (tknSrc?.IsCancellationRequested == true) return;
            options.ObjInfoCallback?.Invoke(objInfo);
        }


        private static bool tryExtract(int trial, LoadOptions options, ObjectInfo objInfo, HashSet<string> done, CancellationTokenSource tknSrc = null) {
            var success = false;
            switch (trial) {
                //first check if there is a match in saved passwords
                case 0 when Setting.MappedPasswords.Rows.Find(options.FilePath) is DataRow row:
                    options.Password = (string)row[nameof(Column.Password)];
                    success = extractZip(options, objInfo, done, tknSrc);
                    break;
                //then try no password
                case 1:
                    options.Password = null;
                    success = extractZip(options, objInfo, done, tknSrc);
                    break;
                //then try all saved passwords with no filename
                case 2:
                    foreach (var fp in Setting.FallbackPasswords) {
                        options.Password = fp;
                        success = extractZip(options, objInfo, done, tknSrc);
                        if (success) break;
                    }
                    break;
                case 3:
                    //if all fails, prompt for password then extract with it
                    if (options.LoadImage &&
                        (options.FileNames == null || options.DecodeSize == default)) {
                        //ask for password when opening explicitly the archive or opening viewer for images inside archive
                        while (!success) {
                            string pwd = null;
                            bool isFb = true;
                            Application.Current.Dispatcher.Invoke(() => {
                                var win = new InputWindow();
                                if (win.ShowDialog() == true) {
                                    pwd = win.TB_Password.Text;
                                    isFb = win.CB_Fallback.IsChecked == true;
                                }
                                win.Close();
                            });

                            if (!string.IsNullOrEmpty(pwd)) {
                                options.Password = pwd;
                                success = extractZip(options, objInfo, done, tknSrc);
                                if (success) {
                                    //make sure the password is saved when task is cancelled
                                    Setting.MappedPasswords.UpdateDataTable(options.FilePath, nameof(Column.Password), pwd);
                                    if (isFb) {
                                        Setting.FallbackPasswords[pwd] = new Observable<string>(pwd);
                                    }
                                    break;
                                }
                            }
                            else break;
                        }
                    }
                    if (!success) {
                        objInfo.Flags |= FileFlags.Error;
                        objInfo.Comments = GetRes("msg_ExtractFailed");
                    }
                    break;
            }
            return success;
        }

        /// <summary>
        /// Returns true or false based on whether extraction succeeds.
        /// </summary>
        private static bool extractZip(LoadOptions options, ObjectInfo objInfo, HashSet<string> done, CancellationTokenSource tknSrc = null) {
            var success = false;
            if (tknSrc?.IsCancellationRequested == true) return success;
            SevenZipExtractor ext = null;
            try {
                ext = options.Password?.Length > 0 ? new SevenZipExtractor(options.FilePath, options.Password) :
                                                     new SevenZipExtractor(options.FilePath);
                //check for multi-volume archives (nope doesnt work)
                //if (ext.VolumeFileNames.Count > 1 && ext.FileName != ext.VolumeFileNames[0]) {
                //    objInfo.Flags = FileFlags.Unknown;
                //    return true;
                //}

                var isThumb = options.DecodeSize == (SizeInt)Setting.ThumbnailSize;
                bool fromDisk = false;

                //get files in archive to extract
                string[] toDo;
                if (options.FileNames?.Length > 0)
                    toDo = options.FileNames;
                else
                    toDo = ext.ArchiveFileData
                        .Where(d => !d.IsDirectory && GetFileType(d.FileName) == FileFlags.Image)
                        .Select(d => d.FileName).ToArray();

                //for archives with encrypted file names, ext.ArchiveFileData will be empty.
                if (toDo == null || toDo.Length == 0) return false;

                //update objInfo
                objInfo.SourcePaths = toDo;

                //iterate over each file and extract when needed
                if (options.LoadImage || options.CldInfoCallback != null || options.ExtractorCallback != null) {
                    foreach (var fileName in toDo) {
                        if (tknSrc?.IsCancellationRequested == true) break;
                        if (!options.Continue) break;

                        //skip if already done
                        if (done.Contains(fileName)) continue;

                        ImageSource source = null;

                        //extractor callback first to ensure it is executed for each file
                        var srcOverride = options.ExtractorCallback?.Invoke(ext, fileName, options);
                        if (srcOverride != null && !options.LoadImage)
                            source = srcOverride;

                        //load image
                        if (options.LoadImage) {
                            if (options.TryCache && isThumb) {
                                //try load from cache
                                source = GetFromThumbDB(options.FilePath, options.DecodeSize, fileName)?.Item1;
                            }
                            if (source == null) {
#if DEBUG
                                Console.WriteLine("Extracting " + fileName);
#endif
                                fromDisk = true;
                                //load from disk
                                using (var ms = new MemoryStream()) {
                                    ext.ExtractFile(fileName, ms);
                                    if (ms.Length == 0) return false;
                                    success = true; //if the task is cancelled, success info is still returned correctly.
                                    source = GetImageSource(ms, options.DecodeSize);
                                }
                                if (isThumb && source != null) AddToThumbDB(source, options.FilePath, fileName, options.DecodeSize);
                            }
                        }
                        
                        //child info callback
                        if (options.CldInfoCallback != null) {
                            var cldInfo = new ObjectInfo(options.FilePath, FileFlags.Image | FileFlags.Archive) {
                                FileName = fileName,
                                SourcePaths = new[] { fileName },
                                ImageSource = source,
                            };
                            if (source == null && options.LoadImage) cldInfo.Flags |= FileFlags.Error;
                            options.CldInfoCallback.Invoke(cldInfo);
                        }
                        
                        done.Add(fileName);
                    }
                }

                //save password for the future
                if (fromDisk && options.Password?.Length > 0) {
                    Setting.MappedPasswords.UpdateDataTable(options.FilePath, nameof(Column.Password), options.Password);
                }

                return true; //it is considered successful if the code reaches here
            }
            catch {
                return false; 
                //if (ex is ExtractionFailedException ||
                //    ex is SevenZipArchiveException ||
                //    ex is NotSupportedException ||
                //    ex is ArgumentOutOfRangeException) return false;

                //if (ext != null) {
                //    ext.Dispose();
                //    ext = null;
                //}
                //throw;
            }
            finally {
                if (ext != null) {
                    ext.Dispose();
                    ext = null;
                }
            }
        }

        public static void ExtractFile(string path, string fileName, Action<ArchiveFileInfo, Stream> callback) {
            ExtractZip(new LoadOptions(path) {
                FileNames = new[] { fileName },
                Flags = FileFlags.Archive,
                LoadImage = false,
                ExtractorCallback = (ext, fn, options) => {
                    MemoryStream ms = null;
                    try {
                        ms = new MemoryStream();
                        ext.ExtractFile(fn, ms);
                        if (ms.Length == 0) return null;
                        callback.Invoke(ext.ArchiveFileData.First(f => f.FileName == fn), ms);
                        return null;
                    }
                    catch { return null; }
                    finally {
                        ms?.Dispose();
                    }
                }
            });
        }

        public static async Task<string[]> GetSourcePathsAsync(ObjectInfo objInfo) {
            return await Task.Run(() => GetSourcePaths(objInfo));
        }

        /// <summary>
        /// <para>If <paramref name="objInfo"/> is a container, fill its SourcePaths with a list of images.</para>
        /// <para>If <paramref name="objInfo"/> is not a container, SourcePaths will be set to a string[1] with its <paramref name="objInfo"/>.FileName in it.</para>
        /// <para>Only udpate when SourcePaths is null. Does not load any images. Call from background threads.</para>
        /// </summary>
        public static string[] GetSourcePaths(ObjectInfo objInfo) {
            if (objInfo.SourcePaths != null) return objInfo.SourcePaths;

            var paths = new string[0];
            switch (objInfo.Flags) {
                case FileFlags.Directory:
                    IEnumerable<FileSystemInfo> fsInfos = null;
                    try {
                        fsInfos = new DirectoryInfo(objInfo.FileSystemPath).EnumerateFileSystemInfos();
                        var srcPaths = new List<string>();
                        foreach (var fsInfo in fsInfos) {
                            var fType = GetPathType(fsInfo);
                            if (fType != FileFlags.Image) continue;
                            srcPaths.Add(fsInfo.Name);
                        }
                        srcPaths.Sort(new NativeHelpers.NaturalStringComparer());
                        paths = srcPaths.ToArray();
                    }
                    catch {
                        objInfo.Flags |= FileFlags.Error;
                    }
                    break;
                case FileFlags.Archive:
                    try {
                        ExtractZip(new LoadOptions(objInfo.FileSystemPath) {
                            Flags = FileFlags.Archive,
                            LoadImage = false,
                            ObjInfoCallback = oi => {
                                if (oi.SourcePaths == null || oi.SourcePaths.Length == 0) return;
                                Array.Sort(oi.SourcePaths, new NativeHelpers.NaturalStringComparer());
                                paths = oi.SourcePaths;
                            }
                        });
                    }
                    catch {
                        objInfo.Flags |= FileFlags.Error;
                    }
                    break;
                case FileFlags.Image:
                //FileFlags.Archive | FileFlags.Image should have SourcePaths[1] set when loaded the first time.
                //this is only included for completeness and should never be reached unless something's wrong with the code.
                case FileFlags.Archive | FileFlags.Image:
                    paths = new[] { objInfo.FileName };
                    break;
            }

            return paths;
        }

        /// <summary>
        /// <para>Async version of <see cref="GetImageSource(ObjectInfo, int, SizeInt, bool)"/> and <see cref="GetImageSource(ObjectInfo, string, SizeInt, bool)"/></para>
        /// <para>If <paramref name="sourcePath"/> is non-null, <paramref name="sourcePathIdx"/> will be ignored.</para>
        /// </summary>
        public static async Task<ImageSource> GetImageSourceAsync(ObjectInfo objInfo,
            string sourcePath = null, int sourcePathIdx = 0, SizeInt decodeSize = default, bool tryCache = true) {
            if (sourcePath == null)
                return await Task.Run(() => GetImageSource(objInfo, sourcePathIdx, decodeSize, tryCache));
            else
                return await Task.Run(() => GetImageSource(objInfo, sourcePath, decodeSize, tryCache));
        }

        /// <summary>
        /// <para>Used to get image source. Returns image source decoded from the source path at the specified index.</para>
        /// <para><paramref name="sourcePathIdx"/> is the index used to get from <paramref name="objInfo"/>.SourcePaths.</para>
        /// <para>Calls <see cref="GetImageSource(ObjectInfo, string, SizeInt, bool)"/> ultimately.</para>
        /// </summary>
        public static ImageSource GetImageSource(ObjectInfo objInfo, int sourcePathIdx, SizeInt decodeSize = default, bool tryCache = true) {
            string sourcePath = null;
            if (objInfo.SourcePaths?.Length > 0) sourcePath = objInfo.SourcePaths[sourcePathIdx];
            return GetImageSource(objInfo, sourcePath, decodeSize, tryCache);
        }

        /// <summary>
        /// <para>Used to get image source. Returns image source decoded from the source path at the specified index.</para>
        /// <para>If <paramref name="sourcePath"/> is null, default icon based on <paramref name="objInfo"/>.Flags will be returned.</para>
        /// </summary>
        public static ImageSource GetImageSource(ObjectInfo objInfo, string sourcePath, SizeInt decodeSize = default, bool tryCache = true) {
            if (objInfo.Flags.HasFlag(FileFlags.Error)) return App.fa_exclamation;
            if (objInfo.Flags == FileFlags.Unknown) return App.fa_file;
#if DEBUG
            var watch = System.Diagnostics.Stopwatch.StartNew();
#endif
            LoadThrottle.Wait();
#if DEBUG
            Console.WriteLine($"Helpers.GetImageSource() waited {watch.ElapsedMilliseconds}ms. Remaining slots: {LoadThrottle.CurrentCount}");
#endif
            ImageSource source = null;
            try {
                //flags is the parent container type
                switch (objInfo.Flags) {
                    case FileFlags.Directory:
                        if (sourcePath != null)
                            source = GetImageSource(Path.Combine(objInfo.FileSystemPath, sourcePath), decodeSize, tryCache) ?? App.fa_image;
                        else
                            source = App.fa_folder;
                        break;
                    case FileFlags.Image:
                        source = GetImageSource(objInfo.FileSystemPath, decodeSize, tryCache) ?? App.fa_image;
                        break;
                    case FileFlags.Archive:
                        if (sourcePath != null) {
                            ExtractZip(new LoadOptions(objInfo.FileSystemPath) {
                                DecodeSize = decodeSize,
                                LoadImage = true,
                                TryCache = tryCache,
                                FileNames = new[] { sourcePath },
                                Flags = FileFlags.Archive,
                                CldInfoCallback = oi => source = oi.ImageSource,
                                ObjInfoCallback = oi => objInfo.Flags = oi.Flags
                            });
                            if (source == null) source = App.fa_image;//CldInfoCallback may not get called when error
                        }
                        else
                            source = App.fa_archive;
                        break;
                    case FileFlags.Archive | FileFlags.Image:
                        //archives are loaded with ImageSource in a single thread
                        //this is only pointing to the source
                        source = objInfo.ImageSource ?? App.fa_image;
                        break;
                }
            }
            catch { }
            finally {
                objInfo = null;
                LoadThrottle.Release();
#if DEBUG
                watch.Stop();
                Console.WriteLine($"Helpers.GetImageSource() exited after {watch.ElapsedMilliseconds}ms. Leaving {LoadThrottle.CurrentCount} slots.");
#endif
            }

            return source;
        }

        /// <summary>
        /// <para>Async version of <see cref="GetImageSource(string, SizeInt, bool)"/></para>
        /// <inheritdoc cref="GetImageSource(string, SizeInt, bool)"/>
        /// </summary>
        public static async Task<BitmapSource> GetImageSourceAsync(string path, SizeInt decodeSize = default, bool tryCache = true) {
            return await Task.Run(() => GetImageSource(path, decodeSize, tryCache));
        }

        /// <summary>
        /// <para>Load image from disk if cache is not availble.</para>
        /// </summary>
        public static BitmapSource GetImageSource(string path, SizeInt decodeSize = default, bool tryCache = true) {
            BitmapSource bs = null;
            var basePath = Path.GetDirectoryName(path);
            var subPath = Path.GetFileName(path);
            try {
                var isThumb = decodeSize == (SizeInt)Setting.ThumbnailSize;
                if (tryCache && isThumb) {
                    //try load from cache when decodeSize is non-zero
                    bs = GetFromThumbDB(basePath, decodeSize, subPath)?.Item1;
                    if (bs != null) return bs;
                }
#if DEBUG
                Console.WriteLine("Loading from disk: " + path);
#endif
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read)) {
                    bs = GetImageSource(fs, decodeSize);
                }
                if (isThumb && bs != null)
                    AddToThumbDB(bs, basePath, subPath, decodeSize);
            }
            catch { }
            
            return bs;
        }

        /// <summary>
        /// Get information about an image such as dimensions and additionally other metadata.
        /// Size will be updated only when <c><paramref name="imgInfo"/>.FileSize</c> is not already set.
        /// Does not dispose the stream.
        /// </summary>
        public static void UpdateImageInfo(Stream stream, ImageInfo imgInfo) {
            if (stream == null || stream.Length == 0) return;
            stream.Position = 0;
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            ushort orien = 0;
            if (frame.Metadata is BitmapMetadata meta) {
                if (meta.GetQuery("/app1/ifd/{ushort=274}") is ushort u) orien = u;
                try { imgInfo.Meta_DateTaken = meta.DateTaken; } catch { }
                try { imgInfo.Meta_ApplicationName = meta.ApplicationName; } catch { }
                try { imgInfo.Meta_Camera = $@"{meta.CameraManufacturer} {meta.CameraModel}"; } catch { }
            }
            if (imgInfo.FileSize == default)
                imgInfo.FileSize = stream.Length;
            imgInfo.Dimensions = orien > 4 ?
                new SizeInt(frame.PixelHeight, frame.PixelWidth) :
                new SizeInt(frame.PixelWidth, frame.PixelHeight);
        }

        /// <summary>
        /// <para>Decode image from stream (FileStream when loading from file or MemoryStream when loading from archive.</para>
        /// <para>A <paramref name="decodeSize"/> higher than the actual resolution will be ignored.
        /// Note that this is the size in pixel instead of the device-independent size used in WPF.</para>
        /// <para>Returns null if error occured.</para>
        /// </summary>
        public static BitmapSource GetImageSource(Stream stream, SizeInt decodeSize = default) {
            try {
                stream.Position = 0;
                var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                var pixelSize = new SizeInt(frame.PixelWidth, frame.PixelHeight);
                ushort orien = 0;
                if ((frame.Metadata as BitmapMetadata)?.GetQuery("/app1/ifd/{ushort=274}") is ushort u)
                    orien = u;
                frame = null;

                //calculate decode size
                if (decodeSize.Width + decodeSize.Height > 0) {
                    //use pixelSize if decodeSize is too big
                    //DecodePixelWidth / Height is set to PixelWidth / Height anyway in reference source
                    if (decodeSize.Width > pixelSize.Width) decodeSize.Width = pixelSize.Width;
                    if (decodeSize.Height > pixelSize.Height) decodeSize.Height = pixelSize.Height;

                    //flip decodeSize according to orientation
                    if (orien > 4 && orien < 9)
                        decodeSize = new SizeInt(decodeSize.Height, decodeSize.Width);
                }

                //init bitmapimage
                stream.Position = 0;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                if (pixelSize.Width > 0 && pixelSize.Height > 0) {
                    //setting both DecodePixelWidth and Height will break the aspect ratio
                    var imgRatio = (double)pixelSize.Width / pixelSize.Height;
                    if (decodeSize.Width > 0 && decodeSize.Height > 0) {
                        if (imgRatio > (double)decodeSize.Width / decodeSize.Height)
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
            catch {
                return null;
            }
        }

        /// <summary>
        /// Get a list of path contents with specified flags.
        /// <paramref name="flags"/> = Image | Archive means the child can be either Image or Archive.
        /// </summary>
        public static IEnumerable<ObjectInfo> GetAll(string path, bool recursive = true, FileFlags flags = FileFlags.Image | FileFlags.Archive) {
            IEnumerable<ObjectInfo> infos = null;

            switch (GetPathType(path)) {
                case FileFlags.Directory:
                    try {
                        var fsInfos = recursive ?
                            new DirectoryInfo(path).EnumerateFileSystemInfos(@"*", SearchOption.AllDirectories) :
                            new DirectoryInfo(path).EnumerateFileSystemInfos();
                        infos = from fsInfo in fsInfos
                                let fType = GetPathType(fsInfo)
                                where flags.HasFlag(fType)
                                select new ObjectInfo(fsInfo.FullName, fType);
                    }
                    catch { }
                    break;
                case FileFlags.Archive:
                    infos = new[] { new ObjectInfo(path, FileFlags.Archive) };
                    break;
            }

            return infos?.OrderBy(i => i.FileSystemPath, new NativeHelpers.NaturalStringComparer());
        }

    }
}
