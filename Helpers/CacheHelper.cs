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

namespace ZipImageViewer
{
    public static class CacheHelper
    {
        //private static CancellationTokenSource tknSrc_BgCache;
        //private static readonly object lock_BgCache = new object();

        //public static void ScanThread() {
        //    while (true) {
        //        while (!Setting.ScanLibrary || Setting.LibraryPaths == null || Setting.LibraryPaths.Count == 0) {
        //            tknSrc_BgCache?.Cancel();
        //            Thread.Sleep(20000);
        //        }

        //        IEnumerable<ObjectInfo> infos = null;
        //        foreach (var path in Setting.LibraryPaths) {
        //            var pathAll = GetAll(path, true, FileFlags.Archive | FileFlags.Image);
        //            if (pathAll == null || pathAll.Count() == 0) continue;

        //            //check if path is all cached??

        //            infos = infos.Concatenate(pathAll);
        //        }
        //        if (infos == null || infos.Count() == 0) return;
        //        CacheObjInfos(infos, ref tknSrc_BgCache, lock_BgCache, false, (s, c, t) => {
        //            Console.WriteLine($"Count: {c}; Total: {t}; Item: {s};");
        //        }, 1);

        //        Thread.Sleep(20000);
        //    }
        //}

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
                    objInfo.SourcePaths = GetSourcePaths(objInfo);
                    if (objInfo.SourcePaths.IsNullOrEmpty()) return;

                    if (objInfo.Flags == FileFlags.Archive) {
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
                    }
                    else {//flag can be Image or Directory
                        if (!ThumbExistInDB(objInfo.ContainerPath, objInfo.SourcePaths[0], decodeSize)) {
                            GetImageSource(objInfo, 0, decodeSize, false);
                        }
                    }
                }
                catch { }
                finally {
                    callback?.Invoke(objInfo.FileSystemPath, Interlocked.Increment(ref count), total);
                }
            }

            //calculate max thread count
            var threadCount = maxThreads > 0 ? MaxLoadThreads / 2 : maxThreads;
            if (threadCount < 1) threadCount = 1;
            //else if (threadCount > 6) threadCount = 6;

            //loop
            try {
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
            }
        }
    }
}
