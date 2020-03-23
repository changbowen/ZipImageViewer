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
        public static void CacheView(string cachePath, bool firstOnly, Window owner = null, MainWindow mainWin = null) {
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

                var infos = firstOnly ?
                    GetAll(cachePath, false, FileFlags.Archive | FileFlags.Image | FileFlags.Directory) :
                    GetAll(cachePath, true, FileFlags.Archive | FileFlags.Image);
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
