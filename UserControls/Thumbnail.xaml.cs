﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using SizeInt = System.Drawing.Size;
using System.Windows.Threading;
using System.Threading;
using System.Windows.Media.Imaging;
using static ZipImageViewer.LoadHelper;

namespace ZipImageViewer
{
    public partial class Thumbnail : UserControl, INotifyPropertyChanged {
        public ObjectInfo ObjectInfo {
            get { return (ObjectInfo)GetValue(ObjectInfoProperty); }
            set { SetValue(ObjectInfoProperty, value); }
        }
        public static readonly DependencyProperty ObjectInfoProperty =
            DependencyProperty.Register("ObjectInfo", typeof(ObjectInfo), typeof(Thumbnail), new PropertyMetadata(null));


        //public Visibility FlagIconVisibility {
        //    get { return (Visibility)GetValue(FlagIconVisibilityProperty); }
        //    set { SetValue(FlagIconVisibilityProperty, value); }
        //}
        //public static readonly DependencyProperty FlagIconVisibilityProperty =
        //    DependencyProperty.Register("FlagIconVisibility", typeof(Visibility), typeof(Thumbnail), new PropertyMetadata(Visibility.Visible));


        //public bool HasImage => thumbImageSource != App.fa_meh &&
        //                        thumbImageSource != App.fa_exclamation &&
        //                        thumbImageSource != App.fa_file &&
        //                        thumbImageSource != App.fa_folder &&
        //                        thumbImageSource != App.fa_archive &&
        //                        thumbImageSource != App.fa_image;

        public event PropertyChangedEventHandler PropertyChanged;

        private int thumbIndex = -1;

        private int thumbTransAnimCount;
        private string thumbTransAnimName;
        private Storyboard thumbTransAnimOut => (Storyboard)FindResource($"{thumbTransAnimName}_Out");
        private Storyboard thumbTransAnimIn => (Storyboard)FindResource($"{thumbTransAnimName}_In");

        private ImageSource nextSource;

        private ImageSource thumbImageSource = App.fa_spinner;
        public ImageSource ThumbImageSource {
            get => thumbImageSource;
            set {
                if (thumbImageSource == value) return;
                nextSource = value;
                if (thumbImageSource == App.fa_spinner)
                    //use simpler animation for initial animation to reduce performance hit
                    thumbTransAnimName = @"SB_ThumbTransInit";
                else
                    thumbTransAnimName = $@"SB_ThumbTrans_{App.Random.Next(0, thumbTransAnimCount)}";

                Dispatcher.Invoke(() => {
                    if (IsLoaded) GR1.BeginStoryboard(thumbTransAnimOut);
                    //fade out
                    //IM1.AnimateDoubleCubicEase(OpacityProperty, 0d, 500, EasingMode.EaseIn,
                    //    completed: (o1, e1) => {
                    //        thumbImageSource = value;
                    //        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbImageSource)));
                    //    });
                    //fade in
                    //IM1.AnimateDoubleCubicEase(OpacityProperty, 1d, 500, EasingMode.EaseOut, begin: 520);
                });
            }
        }

        private MainWindow mainWin;
        private DispatcherTimer cycleTimer;

        public Thumbnail() {
            InitializeComponent();
#if DEBUG
            IM1.SetBinding(ToolTipProperty, new Binding() {
                ElementName = @"TN",
                Path = new PropertyPath($@"{nameof(ObjectInfo)}.{nameof(ObjectInfo.DebugInfo)}"),
                Mode = BindingMode.OneWay,
            });
            ToolTipService.SetShowDuration(IM1, 20000);
#endif
            thumbTransAnimCount = Resources.Keys.Cast<string>().Count(k => k.StartsWith(@"SB_ThumbTrans_")) / 2;

        }

        private void ThumbTransAnimOut_Completed(object sender, EventArgs e) {
            thumbImageSource = nextSource;
            nextSource = null;

            if (thumbImageSource is BitmapSource) {
                //fill frame when it's an actual image
                IM1.Stretch = Stretch.UniformToFill;
                IM1.Width = double.NaN;
                IM1.Height = double.NaN;
            }
            else {
                //half size when it's not an image
                var uniLength = Math.Min(ActualWidth, ActualHeight) * 0.5;
                IM1.Stretch = Stretch.Uniform;
                IM1.Width = uniLength;
                IM1.Height = uniLength;
            }

            //tell binding to update image
            if (PropertyChanged != null) {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbImageSource)));
            }

            //continue transition animation
            GR1.BeginStoryboard(thumbTransAnimIn);
        }

        private void TN_Loaded(object sender, RoutedEventArgs e) {
            var uniLength = Math.Min(ActualWidth, ActualHeight) * 0.5;
            IM1.Stretch = Stretch.Uniform;
            IM1.Width = uniLength;
            IM1.Height = uniLength;

            cycleTimer = new DispatcherTimer(DispatcherPriority.Normal, Application.Current.Dispatcher);
            cycleTimer.Tick += cycleImageSource;

            mainWin = (MainWindow)Window.GetWindow(this);

            cycleImageSource(null, null);
            //var objInfo = ObjectInfo;
            //Task.Run(() => {
            //    if (objInfo.SourcePaths == null) {//non-null indicate SourcePaths has already been updated
            //        var now = System.Diagnostics.Stopwatch.StartNew();
            //        UpdateSourcePaths(objInfo);
            //        now.Stop();
            //        Console.WriteLine(now.ElapsedMilliseconds);
            //    }
            //    //Dispatcher.Invoke(() => cycleImageSource(null, null));
            //});
        }

        private void TN_Unloaded(object sender, RoutedEventArgs e) {
            //ThumbImageSource = null;
            //if (ObjectInfo != null) {
            //    ObjectInfo.ImageSources = null;
            //    ObjectInfo = null;
            //}
            //nextSource = null;
            //if (IM1 != null) {
            //    IM1.Source = null;
            //    IM1.ToolTip = null;
            //    IM1 = null;
            //}
            //mask = null;
            cycleTimer.Stop();
            cycleTimer.Tick -= cycleImageSource;
        }

        private static int workingThreads = 0;

        private async void cycleImageSource(object sender, EventArgs e) {
            var tn = this;
            tn.cycleTimer.Stop();

            //dont do anything before or after the lifecycle, or if not loaded in virtualizing panel
            if (!tn.IsLoaded) return;
            if (tn.ObjectInfo == null) return;

            //update source paths if needed
            if (tn.ObjectInfo.SourcePaths == null) {
                var objInfo = tn.ObjectInfo;
                await Task.Run(() => UpdateSourcePaths(objInfo));
            }

            //get the next path index to use
            var cycle = false;
            if (tn.ObjectInfo.SourcePaths?.Length > 1) {
                tn.thumbIndex = tn.thumbIndex == tn.ObjectInfo.SourcePaths.Length - 1 ? 0 : tn.thumbIndex + 1;
                cycle = true;
            }
            else
                tn.thumbIndex = 0;

            //wait to get image
            if (Interlocked.CompareExchange(ref workingThreads, 0, 0) >= MaxLoadThreads) {
                tn.cycleTimer.Interval = TimeSpan.FromMilliseconds(200);
                tn.cycleTimer.Start();
                return;
            }
            Interlocked.Increment(ref workingThreads);
            tn.ThumbImageSource = await GetImageSourceAsync(tn.ObjectInfo, tn.thumbIndex, decodeSize: (SizeInt)Setting.ThumbnailSize);
            Interlocked.Decrement(ref workingThreads);

            //dont do anything before or after the lifecycle
            if (!tn.IsLoaded || !mainWin.IsLoaded || !cycle) return;

            //plan for the next run
            var delay = mainWin.ThumbChangeDelay;
            tn.cycleTimer.Interval = TimeSpan.FromMilliseconds(delay);
            tn.cycleTimer.Start();
        }
    }
}
