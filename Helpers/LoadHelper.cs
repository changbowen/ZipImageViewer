using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SizeInt = System.Drawing.Size;
using static ZipImageViewer.Helpers;
using System.Threading;
using static ZipImageViewer.TableHelper;
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
            public bool TryCache { get; set; } = true;

            public LoadOptions(string filePath) {
                FilePath = filePath;
            }
        }

        public static readonly int MaxLoadThreads = Environment.ProcessorCount;
        public static SemaphoreSlim LoadThrottle = new SemaphoreSlim(MaxLoadThreads);

        /// <summary>
        /// Load image based on the type of file and try passwords when possible.
        /// <para>
        /// If filePath points to an archive, ObjectInfo.Flags in ObjInfoCallback will contain FileFlag.Error when extraction fails.
        /// ObjectInfo.SourcePaths in ObjInfoCallback contains the file list inside archive.
        /// ObjectInfo in CldInfoCallback contains information for files inside archive.
        /// </para>
        /// Should be called from a background thread.
        /// Callback can be used to manipulate the loaded images. For e.g. display it in the ViewWindow, or add to ObjectList as thumbnails.
        /// Callback is called for each image loaded.
        /// Use Dispatcher if callback needs to access the UI thread.
        /// <param name="flags">Only checks for Image and Archive.</param>
        /// </summary>
        internal static void LoadFile(LoadOptions options, CancellationTokenSource tknSrc = null) {
            if (tknSrc?.IsCancellationRequested == true) return;

            //objInfo to be returned
            var objInfo = new ObjectInfo(options.FilePath, options.Flags) {
                FileName = Path.GetFileName(options.FilePath)
            };

            //when file is an image
            if (options.Flags.HasFlag(FileFlags.Image) && !options.Flags.HasFlag(FileFlags.Archive)) {
                objInfo.SourcePaths = new[] { options.FilePath };
                if (options.LoadImage)
                    objInfo.ImageSource = GetImageSource(options.FilePath, options.DecodeSize);
            }
            //when file is an archive
            else if (options.Flags.HasFlag(FileFlags.Archive)) {
                //some files may get loaded from cache therefore unaware of whether password is correct
                //the HashSet records processed files through retries
                var done = new HashSet<string>();
                for (int caseIdx = 0; caseIdx < 4; caseIdx++) {
                    if (tknSrc?.IsCancellationRequested == true) break;

                    var success = false;
                    switch (caseIdx) {
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
                                objInfo.Comments = $"Extraction failed. Bad password or not supported image formats.";
                            }
                            break;
                    }

                    if (success) break;
                }
            }

            if (tknSrc?.IsCancellationRequested == true) return;
            options.ObjInfoCallback?.Invoke(objInfo);
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
                var isThumb = options.DecodeSize == (SizeInt)Setting.ThumbnailSize;
                bool fromDisk = false;

                //get files in archive to extract
                string[] toDo;
                if (options.FileNames?.Length > 0)
                    toDo = options.FileNames;
                else
                    toDo = ext.ArchiveFileData
                        .Where(d => !d.IsDirectory && GetPathType(d.FileName) == FileFlags.Image)
                        .Select(d => d.FileName).ToArray();

                foreach (var fileName in toDo) {
                    if (tknSrc?.IsCancellationRequested == true) break;

                    //skip if already done
                    if (done.Contains(fileName)) continue;

                    ImageSource source = null;
                    if (options.LoadImage) {
                        var thumbPathInDb = Path.Combine(options.FilePath, fileName);
                        if (options.TryCache && isThumb) {
                            //try load from cache
                            source = SQLiteHelper.GetFromThumbDB(thumbPathInDb, options.DecodeSize);
                        }
                        if (source == null) {
#if DEBUG
                            Console.WriteLine("Extracting " + fileName);
#endif
                            fromDisk = true;
                            //load from disk
                            using (var ms = new MemoryStream()) {
                                ext.ExtractFile(fileName, ms);
                                success = true; //if the task is cancelled, success info is still returned correctly.
                                source = GetImageSource(ms, options.DecodeSize);
                            }
                            if (isThumb && source != null) SQLiteHelper.AddToThumbDB(source, thumbPathInDb, options.DecodeSize);
                        }
                    }

                    if (options.CldInfoCallback != null) {
                        var cldInfo = new ObjectInfo(options.FilePath, FileFlags.Image | FileFlags.Archive) {
                            FileName = fileName,
                            SourcePaths = new[] { fileName },
                            ImageSource = source,
                        };
                        options.CldInfoCallback.Invoke(cldInfo);
                    }

                    done.Add(fileName);
                }

                //update objInfo
                objInfo.SourcePaths = toDo;

                //save password for the future
                if (fromDisk && options.Password?.Length > 0) {
                    Setting.MappedPasswords.UpdateDataTable(options.FilePath, nameof(Column.Password), options.Password);
                }

                return true; //it is considered successful if the code reaches here
            }
            catch (Exception ex) {
                if (ex is ExtractionFailedException ||
                    ex is SevenZipArchiveException ||
                    ex is NotSupportedException) return false;

                if (ext != null) {
                    ext.Dispose();
                    ext = null;
                }
                throw;
            }
            finally {
                if (ext != null) {
                    ext.Dispose();
                    ext = null;
                }
            }
        }

        public static void ExtractFile(string path, string fileName, Action<ArchiveFileInfo, Stream> callback) {
            SevenZipExtractor ext = null;
            try {
                for (int caseIdx = 0; caseIdx < 3; caseIdx++) {
                    var success = false;
                    switch (caseIdx) {
                        case 0 when Setting.MappedPasswords.Rows.Find(path) is DataRow row:
                            success = extractFile(path, fileName, callback, (string)row[nameof(Column.Password)]);
                            break;
                        case 1:
                            success = extractFile(path, fileName, callback);
                            break;
                        case 2:
                            foreach (var fp in Setting.FallbackPasswords) {
                                success = extractFile(path, fileName, callback, fp);
                                if (success) break;
                            }
                            break;
                    }
                    
                    if (success) break;
                }
            }
            catch { }
            finally {
                ext?.Dispose();
            }
        }

        private static bool extractFile(string path, string fileName, Action<ArchiveFileInfo, Stream> callback, string password = null) {
            SevenZipExtractor ext = null;
            try {
                ext = password?.Length > 0 ?
                new SevenZipExtractor(path, password) :
                new SevenZipExtractor(path);
                using (var ms = new MemoryStream()) {
                    ext.ExtractFile(fileName, ms);
                    callback.Invoke(ext.ArchiveFileData.First(f => f.FileName == fileName), ms);
                }
                return true;
            }
            catch {
                return false;
            }
            finally {
                ext?.Dispose();
            }
        }

        /// <summary>
        /// Only udpate when SourcePaths is null. Call from background thread.
        /// This is faster than you think.
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
                    LoadFile(new LoadOptions(objInfo.FileSystemPath) {
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
        public static async Task<ImageSource> GetImageSourceAsync(ObjectInfo objInfo, int sourcePathIdx, SizeInt decodeSize = default, bool tryCache = true) {
            return await Task.Run(() => GetImageSource(objInfo, sourcePathIdx, decodeSize, tryCache));
        }

        /// <summary>
        /// Used to get image from within a container.
        /// </summary>
        /// <param name="decodeSize">Decode size.</param>
        public static ImageSource GetImageSource(ObjectInfo objInfo, int sourcePathIdx, SizeInt decodeSize = default, bool tryCache = true) {
            if (objInfo.Flags.HasFlag(FileFlags.Error)) return App.fa_exclamation;
            if (objInfo.Flags == FileFlags.Unknown) return App.fa_file;

#if DEBUG
            var now = DateTime.Now;
#endif
            LoadThrottle.Wait();
#if DEBUG
            Console.WriteLine($"Helpers.GetImageSource() waited {(DateTime.Now - now).TotalMilliseconds}ms. Remaining slots: {LoadThrottle.CurrentCount}");
#endif

            ImageSource source = null;
            try {
                //flags is the parent container type
                switch (objInfo.Flags) {
                    case FileFlags.Directory:
                        if (objInfo.SourcePaths?.Length > 0)
                            source = GetImageSource(objInfo.SourcePaths[sourcePathIdx], decodeSize, tryCache);
                        if (source == null) {
                            source = App.fa_folder;
                        }
                        break;
                    case FileFlags.Image:
                        source = GetImageSource(objInfo.FileSystemPath, decodeSize, tryCache);
                        if (source == null) {
                            source = App.fa_image;
                        }
                        break;
                    case FileFlags.Archive:
                        if (objInfo.SourcePaths?.Length > 0) {
                            LoadFile(new LoadOptions(objInfo.FileSystemPath) {
                                DecodeSize = decodeSize,
                                LoadImage = true,
                                TryCache = tryCache,
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
            catch { }
            finally {
                objInfo = null;

                LoadThrottle.Release();
#if DEBUG
                Console.WriteLine($"Helpers.GetImageSource() exited leaving {LoadThrottle.CurrentCount} slots.");
#endif
            }
            return source;
        }

        /// <summary>
        /// <para>Load image from disk if cache is not availble.</para>
        /// </summary>
        public static BitmapSource GetImageSource(string path, SizeInt decodeSize = default, bool tryCache = true) {
            BitmapSource bs = null;
            var isThumb = decodeSize == (SizeInt)Setting.ThumbnailSize;
            if (tryCache && isThumb) {
                //try load from cache when decodeSize is non-zero
                bs = SQLiteHelper.GetFromThumbDB(path, decodeSize);
                if (bs != null) return bs;
            }
#if DEBUG
            Console.WriteLine("Loading from disk: " + path);
#endif
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read)) {
                bs = GetImageSource(fs, decodeSize);
            }
            if (isThumb && bs != null)
                SQLiteHelper.AddToThumbDB(bs, path, decodeSize);

            return bs;
        }

        public static void UpdateImageInfo(Stream stream, ImageInfo imgInfo) {
            stream.Position = 0;
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            ushort orien = 0;
            if (frame.Metadata is BitmapMetadata meta) {
                if (meta.GetQuery("/app1/ifd/{ushort=274}") is ushort u) orien = u;
                imgInfo.Meta_DateTaken = meta.DateTaken;
                imgInfo.Meta_ApplicationName = meta.ApplicationName;
                imgInfo.Meta_Camera = $@"{meta.CameraManufacturer} {meta.CameraModel}";
            }
            imgInfo.Dimensions = orien > 4 ?
                new SizeInt(frame.PixelHeight, frame.PixelWidth) :
                new SizeInt(frame.PixelWidth, frame.PixelHeight);
        }

        /// <summary>
        /// <para>Decode image from stream (FileStream when loading from file or MemoryStream when loading from archive.</para>
        /// <para>A <paramref name="decodeSize"/> higher than the actual resolution will be ignored.
        /// Note that this is the size in pixel instead of the device-independent size used in WPF.</para>
        /// </summary>
        public static BitmapSource GetImageSource(Stream stream, SizeInt decodeSize = default) {
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

        public static void CacheFolder(string folderPath, ref CancellationTokenSource tknSrc, object tknLock, Action<string, int, int> callback) {
            tknSrc?.Cancel();
            tknSrc?.Dispose();
            Monitor.Enter(tknLock);
            tknSrc = new CancellationTokenSource();

            var decodeSize = (SizeInt)Setting.ThumbnailSize;
            var threadCount = MaxLoadThreads / 2;
            if (threadCount < 1) threadCount = 1;
            else if (threadCount > 6) threadCount = 6;
            var paraOptions = new ParallelOptions() {
                CancellationToken = tknSrc.Token,
                MaxDegreeOfParallelism = threadCount,
            };
            var count = 0;
            try {
                var infos = new DirectoryInfo(folderPath).EnumerateFileSystemInfos();
                var total = infos.Count();
                Parallel.ForEach(infos, paraOptions, (info, state) => {
                    if (paraOptions.CancellationToken.IsCancellationRequested) state.Break();
                    var flag = GetPathType(info);
                    var objInfo = new ObjectInfo(info.FullName, flag) {
                        FileName = info.Name,
                    };
                    try {
                        UpdateSourcePaths(objInfo);
                        if (objInfo.SourcePaths?.Length > 0) {
                            var path = objInfo.Flags.HasFlag(FileFlags.Archive) ?
                                Path.Combine(objInfo.FileSystemPath, objInfo.SourcePaths[0]) : objInfo.SourcePaths[0];
                            if (!SQLiteHelper.ThumbExistInDB(path, decodeSize)) {
                                GetImageSource(objInfo, 0, decodeSize, false);
                            }
                        }
                    }
                    catch { }
                    finally {
                        callback?.Invoke(info.FullName, Interlocked.Increment(ref count), total);
                    }
                    if (paraOptions.CancellationToken.IsCancellationRequested) state.Break();
                });
            }
            catch (OperationCanceledException) { }
            finally {
                tknSrc.Dispose();
                tknSrc = null;
                Monitor.Exit(tknLock);
            }

            //            while (cacheThreadIdx < ObjectList.Count) {
            //                while (tknSrc_LoadThumb != null || LoadThrottle.CurrentCount <= 1) {
            //                    if (cacheThreadExit) break;
            //                    Thread.Sleep(2000);
            //                }

            //                var objInfo = ObjectList[cacheThreadIdx];
            //                var decodeSize = (SizeInt)Setting.ThumbnailSize;
            //                if (objInfo.SourcePaths == null) UpdateSourcePaths(objInfo);
            //                var path = objInfo.Flags.HasFlag(FileFlags.Archive) ?
            //                    Path.Combine(objInfo.FileSystemPath, objInfo.SourcePaths[0]) :
            //                    objInfo.SourcePaths[0];
            //                if (!SQLiteHelper.ThumbExistInDB(path, decodeSize)) {
            //#if DEBUG
            //                    Console.WriteLine($"Caching to DB: {path}");
            //#endif
            //                    GetImageSource(objInfo, 0, decodeSize, false);
            //                }

            //                if (cacheThreadExit) break;
            //                cacheThreadIdx += 1;
            //            }
        }
    }
}
