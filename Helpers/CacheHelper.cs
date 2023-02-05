using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SizeInt = System.Drawing.Size;
using static ZipImageViewer.Helpers;
using static ZipImageViewer.LoadHelper;
using static ZipImageViewer.SQLiteHelper;
using System.Windows.Media;
using System.IO;
using System.Windows;

namespace ZipImageViewer
{
    public static class CacheHelper
    {
        /// <param name="mainWin">If not null, MainWindow will be halted before caching is finished.</param>
        public static void CachePath(string cachePath, bool firstOnly, Window owner = null, MainWindow mainWin = null) {
            var bw = new BlockWindow(owner) {
                MessageTitle = GetRes("msg_Processing")
            };
            //callback used to update progress
            Action<string, int, int> cb = (path, i, count) => {
                var p = (int)Math.Floor((double)i / count * 100);
                Application.Current.Dispatcher.Invoke(() => {
                    bw.Percentage = p;
                    bw.MessageBody = path;
                    if (bw.Percentage == 100) bw.MessageTitle = GetRes("ttl_OperationComplete");
                });
            };

            //work thread
            bw.Work = () => {
                if (mainWin != null) {
                    mainWin.tknSrc_LoadThumb?.Cancel();
                    while (mainWin.tknSrc_LoadThumb != null) {
                        Thread.Sleep(200);
                    }
                    mainWin.preRefreshActions();
                }

                //because cache always needs to cache all images under current view, we need to get containers plus images under root
                IEnumerable<ObjectInfo> infos = null;
                var dirInfo = new DirectoryInfo(cachePath);
                switch (GetPathType(dirInfo)) {
                    case FileFlags.Directory:
                        infos = dirInfo.EnumerateFiles()
                            .Where(fi => GetPathType(fi) == FileFlags.Image)
                            .Select(fi => new ObjectInfo(fi.FullName, FileFlags.Image, fi.Name))
                            .Concatenate(EnumerateContainers(cachePath, inclRoot: false));
                        break;
                    case FileFlags.Archive:
                        infos = new[] { new ObjectInfo(cachePath, FileFlags.Archive) };
                        break;
                }
                
                CacheObjInfos(infos, ref bw.tknSrc_Work, bw.lock_Work, firstOnly, cb);

                if (mainWin != null)
                    Task.Run(() => mainWin.LoadPath(mainWin.CurrentPath));
            };
            bw.FadeIn();
        }


        public static void CacheObjInfos(IEnumerable<ObjectInfo> infos, ref CancellationTokenSource tknSrc, object tknLock, bool firstOnly,
            Action<string, int, int> callback = null, int maxThreads = 0) {

            tknSrc?.Cancel();
            Monitor.Enter(tknLock);
            tknSrc = new CancellationTokenSource();
            var tknSrcLocal = tknSrc; //for use in lambda
            var count = 0;
            var decodeSize = (SizeInt)Setting.ThumbnailSize;

            var total = infos.Count();

            void cacheObjInfo(ObjectInfo objInfo) {
                try {
                    switch (objInfo.Flags) {
                        case FileFlags.Directory:
                        case FileFlags.Image:
                            objInfo.SourcePaths = GetSourcePaths(objInfo);
                            if (objInfo.SourcePaths == null || objInfo.SourcePaths.Length == 0) break;
                            if (objInfo.Flags == FileFlags.Directory && firstOnly)
                                objInfo.SourcePaths = new[] { objInfo.SourcePaths[0] };
                            foreach (var srcPath in objInfo.SourcePaths) {
                                if (!ThumbExistInDB(objInfo.ContainerPath, srcPath, decodeSize)) {
                                    GetImageSource(objInfo, srcPath, decodeSize, false);
                                }
                            }
                            break;
                        case FileFlags.Archive:
                            ExtractZip(new LoadOptions(objInfo.FileSystemPath) {
                                Flags = FileFlags.Archive,
                                LoadImage = false,
                                DecodeSize = decodeSize,
                                ExtractorCallback = (ext, fileName, options) => {
                                    try {
                                        if (ThumbExistInDB(ext.FileName, fileName, decodeSize)) {
                                            if (firstOnly) options.Continue = false;
                                            return null;
                                        }
                                        ImageSource source = null;
                                        using (var ms = new MemoryStream()) {
                                            ext.ExtractFile(fileName, ms);
                                            if (ms.Length > 0)
                                                source = GetImageSource(ms, decodeSize);
                                        }
                                        if (source != null) {
                                            AddToThumbDB(source, objInfo.FileSystemPath, fileName, decodeSize);
                                            if (firstOnly) options.Continue = false;
                                        }
                                    }
                                    catch { }
                                    finally {
                                        callback?.Invoke(fileName, count, total);
                                    }
                                    return null;
                                },
                            }, tknSrcLocal);
                            break;
                    }
                }
                catch { }
                finally {
                    callback?.Invoke(objInfo.FileSystemPath, Interlocked.Increment(ref count), total);
                }
            }

            //calculate max thread count
            var threadCount = maxThreads > 0 ? maxThreads : MaxLoadThreads / 2;
            if (threadCount < 1) threadCount = 1;
            else if (threadCount > MaxLoadThreads) threadCount = MaxLoadThreads;

            //loop
            try {
                //avoid sleep
                NativeHelpers.SetPowerState(1);

                if (threadCount == 1) {
                    foreach (var objInfo in infos) {
                        if (tknSrc?.IsCancellationRequested == true) break;
                        cacheObjInfo(objInfo);
                    }
                }
                else {
                    var paraOptions = new ParallelOptions() {
                        CancellationToken = tknSrc.Token,
                        MaxDegreeOfParallelism = threadCount,
                    };
                    Parallel.ForEach(infos, paraOptions, (objInfo, state) => {
                        if (paraOptions.CancellationToken.IsCancellationRequested) state.Break();
                        cacheObjInfo(objInfo);
                        if (paraOptions.CancellationToken.IsCancellationRequested) state.Break();
                    });
                }
            }
            catch { }
            finally {
                tknSrc.Dispose();
                tknSrcLocal = null;
                tknSrc = null;
                Monitor.Exit(tknLock);
                NativeHelpers.SetPowerState(0);
            }
        }
    }
}
