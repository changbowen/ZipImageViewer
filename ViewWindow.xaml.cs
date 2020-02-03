using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static ZipImageViewer.ExtentionMethods;

namespace ZipImageViewer
{
    public struct TransParam
    {
        public double Double1 { get; set; }
        public double Double2 { get; set; }
        public Duration Duration1 { get; set; }
        
        /// <param name="dur1">Value in milliseconds for Duration1.</param>
        public TransParam(double dbl1 = default, double dbl2 = default, int dur1 = default) {
            Double1 = dbl1;
            Double2 = dbl2;
            Duration1 = new Duration(TimeSpan.FromMilliseconds(dur1));
        }
    }

    public partial class ViewWindow : Window, INotifyPropertyChanged
    {
        public ObjectInfo ObjectInfo
        {
            get { return (ObjectInfo)GetValue(ObjectInfoProperty); }
            set { SetValue(ObjectInfoProperty, value); }
        }
        public static readonly DependencyProperty ObjectInfoProperty =
            Thumbnail.ObjectInfoProperty.AddOwner(typeof(ViewWindow));


        public ImageSource ViewImageSource {
            get { return (ImageSource)GetValue(ViewImageSourceProperty); }
            set { SetValue(ViewImageSourceProperty, value); }
        }
        public static readonly DependencyProperty ViewImageSourceProperty =
            DependencyProperty.Register("ViewImageSource", typeof(ImageSource), typeof(ViewWindow), new PropertyMetadata(null));


        public bool Transforming {
            get { return (bool)GetValue(TransformingProperty); }
            set { SetValue(TransformingProperty, value); }
        }
        public static readonly DependencyProperty TransformingProperty =
            DependencyProperty.Register("Transforming", typeof(bool), typeof(ViewWindow),
                new PropertyMetadata(false, new PropertyChangedCallback(TransformingChanged)));

        private static void TransformingChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e) {

        }

        private Tuple<TransParam, TransParam> transParams;
        /// <summary>
        /// Item1 is used in "out" animations (before changing ImageSource).
        /// Item2 is used in "in" animations (after changing ImageSource).
        /// </summary>
        public Tuple<TransParam, TransParam> TransParams {
            get => transParams;
            set {
                transParams = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TransParams)));
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        private double Scale = 1d;

        //public Point CenterPoint =>
        //    new Point((CA.ActualWidth - IM.ActualWidth) / 2, (CA.ActualHeight - IM.ActualHeight) / 2);

        public ViewWindow()
        {
            Opacity = 0d;
            InitializeComponent();
        }

        private void ViewWindow_Loaded(object sender, RoutedEventArgs e) {
            scaleToCanvas();
            ViewWin.AnimateDoubleCubicEase(OpacityProperty, 1d, 500, EasingMode.EaseOut);
        }

        private void ViewWin_MouseUp(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Right) Close();
        }

        private void transform(int ms, Size? newSize = null, Point? transPoint = null) {
            if (newSize.HasValue) {
                newSize = new Size(newSize.Value.Width, newSize.Value.Height);
                if (double.IsNaN(IM.Width) || double.IsNaN(IM.Height)) {
                    //skip animation when Width or Height is not set
                    IM.Width = newSize.Value.Width;
                    IM.Height = newSize.Value.Height;
                }
                else {
                    IM.AnimateDoubleCubicEase(WidthProperty, newSize.Value.Width, ms, EasingMode.EaseOut);
                    IM.AnimateDoubleCubicEase(HeightProperty, newSize.Value.Height, ms, EasingMode.EaseOut);
                }
                Scale = newSize.Value.Width / IM.RealSize.Width;
                BM.Show($"{Scale:P1}");
            }
            if (transPoint.HasValue) {
                transPoint = new Point(transPoint.Value.X.RoundToMultiplesOf(IM.TransformFromDevice.M11),
                                       transPoint.Value.Y.RoundToMultiplesOf(IM.TransformFromDevice.M22));
                IM_TT.AnimateDoubleCubicEase(TranslateTransform.XProperty, transPoint.Value.X, ms, EasingMode.EaseOut);
                IM_TT.AnimateDoubleCubicEase(TranslateTransform.YProperty, transPoint.Value.Y, ms, EasingMode.EaseOut);
            }
            if (ms > 0) {
                this.AnimateBool(TransformingProperty, true, false, ms);
            }
        }

        private void scaleToCanvas(int ms = 400) {
            var uniSize = Helpers.UniformScale(IM.RealSize, new Size(CA.ActualWidth, CA.ActualHeight));
            transform(ms, uniSize, new Point(0d, 0d));
        }

        private void scaleCenterMouse(Point relativePos, Size targetSize, int ms = 400) {
            //the point ensures pixels around mouse point not moving
            transform(ms, targetSize,
                          new Point(IM_TT.X + (IM.Width / 2d - relativePos.X) * (targetSize.Width / IM.Width - 1d),
                                    IM_TT.Y + (IM.Height/ 2d - relativePos.Y) * (targetSize.Height/ IM.Height - 1d)));
        }


        private Point mouseCapturePoint;
        private Matrix existingTranslate;


        private void CA_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 1) {
                mouseCapturePoint = e.GetPosition(CA);
                existingTranslate = IM_TT.Value;
                CA.CaptureMouse();
            }
            else if (e.ClickCount == 2) {
                if (!IM.IsRealSize)
                    scaleCenterMouse(e.GetPosition(IM), IM.RealSize);
                else
                    scaleToCanvas();

                ////use different animation when image is large to reduce stutter
                //if (rs.Width / CA.ActualWidth < 1.8d && rs.Height / CA.ActualHeight < 1.8d)
                //    Transform(ms, tgtSize, tgtPoint);
                //else
                //    Task.Run(() => {
                //        Dispatcher.Invoke(() => IM.AnimateDoubleCubicEase(OpacityProperty, 0.01d, 100, EasingMode.EaseOut));
                //        Thread.Sleep(100);
                //        Dispatcher.Invoke(() => Transform(0, tgtSize, tgtPoint));
                //        Thread.Sleep(100);
                //        Dispatcher.Invoke(() => IM.AnimateDoubleCubicEase(OpacityProperty, 1d, 200, EasingMode.EaseOut), DispatcherPriority.Background);
                //    });
            }
        }

        private void CA_PreviewMouseMove(object sender, MouseEventArgs e) {
            if (!CA.IsMouseCaptured) return;
            transform(50, transPoint:
                new Point(existingTranslate.OffsetX + ((e.GetPosition(CA).X - mouseCapturePoint.X) * Scale * 2d).RoundToMultiplesOf(IM.TransformFromDevice.M11),
                          existingTranslate.OffsetY + ((e.GetPosition(CA).Y - mouseCapturePoint.Y) * Scale * 2d).RoundToMultiplesOf(IM.TransformFromDevice.M22)));
        }

        private void CA_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            CA.ReleaseMouseCapture();
        }


        private void CA_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            //if (Transforming) return;
            var scale = e.Delta > 0 ? 1.25d : 0.8d;
            scaleCenterMouse(e.GetPosition(IM), new Size(IM.Width * scale, IM.Height * scale), 80);
        }

        private void CA_PreviewKeyUp(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Left:
                case Key.Right:
                case Key.Space:
                    var list = App.MainWin.ObjectList;
                    //get direction for the next items.
                    //also used to determine direction for some animations.
                    var increment = e.Key == Key.Left ? -1 : 1;
                    //get index of the next item
                    var i = list.IndexOf(list[ObjectInfo.VirtualPath]) + increment;
                    while (i > -1 && i < list.Count) {
                        var next = list[i];
                        //check for non-images and skip
                        if (!next.Flags.HasFlag(FileFlags.Image)) {
                            i += increment;
                            continue;
                        }
                        //out animation
                        var trans = Setting.ViewerTransition;
                        if (trans == Setting.Transition.Random) {
                            var transVals = Enum.GetValues(typeof(Setting.Transition));
                            trans = (Setting.Transition)transVals.GetValue(App.Random.Next(2, transVals.Length));
                        }
                        switch (trans) {
                            case Setting.Transition.ZoomFadeBlur:
                                TransParams = new Tuple<TransParam, TransParam>(
                                    new TransParam(1 - 0.05 * increment, dur1: 200),
                                    new TransParam(dur1: 500));
                                break;
                            case Setting.Transition.Fade:
                                TransParams = new Tuple<TransParam, TransParam>(
                                    new TransParam(dur1: 200),
                                    new TransParam(dur1: 500));
                                break;
                            case Setting.Transition.HorizontalSwipe:
                                //bound to From, To and Duration respectively
                                TransParams = new Tuple<TransParam, TransParam>(
                                    new TransParam(0d, (IM_TT.X - IM.Width / 2d) * increment, 400),
                                    new TransParam(IM.Width / 2d * increment, 0d, 500));
                                break;
                            case Setting.Transition.None:
                                break;
                        }
                        if (trans != Setting.Transition.None) {
                            IM.BeginStoryboard((Storyboard)IM.FindResource($"SB_Trans_{trans}_Out"));
                            this.AnimateBool(TransformingProperty, true, false, (int)TransParams.Item1.Duration1.TimeSpan.TotalMilliseconds);
                        }

                        //load next or previous image
                        Task.Run(() => {
                            if (TransParams != null && TransParams.Item1.Duration1.HasTimeSpan)
                                Thread.Sleep((int)TransParams.Item1.Duration1.TimeSpan.TotalMilliseconds);
                            App.MainWin.LoadPath(next, this);
                            Dispatcher.Invoke(() => {
                                //reset image position instantaneously
                                scaleToCanvas(0);
                                //in animation
                                if (trans != Setting.Transition.None)
                                    IM.BeginStoryboard((Storyboard)IM.FindResource($"SB_Trans_{trans}_In"));
                            }, DispatcherPriority.ApplicationIdle);
                        });
                        return;
                    }

                    BM.Show("No more!");
                    break;
            }
        }

    }
}
