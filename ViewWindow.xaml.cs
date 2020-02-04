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
            Thumbnail.ObjectInfoProperty.AddOwner(typeof(ViewWindow), new PropertyMetadata(new PropertyChangedCallback(ObjectInfoChanged)));

        private static void ObjectInfoChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e) {
            var win = (ViewWindow)obj;
            if (!win.IsLoaded)
                //update directly without animations when window is not loaded yet (first time opening)
                win.ViewImageSource = win.ObjectInfo.ImageSources[0];
            else if (win.IM.Transforming)
                //update after transform end
                DependencyPropertyDescriptor.FromProperty(DpiImage.TransformingProperty, typeof(DpiImage)).AddValueChanged(win.IM, updateViewImageSource);
            else
                //update with "in" animations following the previous "out" animations
                updateViewImageSource(win, null);
        }

        private static void updateViewImageSource(object obj, EventArgs e) {
            var win = (ViewWindow)obj;
            DependencyPropertyDescriptor.FromProperty(DpiImage.TransformingProperty, typeof(DpiImage)).RemoveValueChanged(win.IM, updateViewImageSource);
            //update image
            win.ViewImageSource = win.ObjectInfo.ImageSources[0];
            //reset image position instantaneously
            win.scaleToCanvas(0);
            //continue "in" animation
            if (win.LastTransition != Setting.Transition.None)
                win.IM.BeginStoryboard((Storyboard)win.IM.FindResource($"SB_Trans_{win.LastTransition}_In"));
        }


        public ImageSource ViewImageSource {
            get { return (ImageSource)GetValue(ViewImageSourceProperty); }
            set { SetValue(ViewImageSourceProperty, value); }
        }
        public static readonly DependencyProperty ViewImageSourceProperty =
            DependencyProperty.Register("ViewImageSource", typeof(ImageSource), typeof(ViewWindow), new PropertyMetadata(null));


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

        private Setting.Transition LastTransition = Setting.Transition.None;

        //public Point CenterPoint =>
        //    new Point((CA.ActualWidth - IM.ActualWidth) / 2, (CA.ActualHeight - IM.ActualHeight) / 2);

        public ViewWindow()
        {
            Opacity = 0d;
            InitializeComponent();
        }

        private void ViewWindow_Loaded(object sender, RoutedEventArgs e) {
            scaleToCanvas(0);
            this.AnimateDoubleCubicEase(OpacityProperty, 1d, 100, EasingMode.EaseOut);
        }

        private void ViewWin_MouseUp(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Right) Close();
        }

        /// <param name="altAnim">Set this to true or false to override alternate zoom & move animation.</param>
        private void transform(int ms, Size? newSize = null, Point? transPoint = null, bool? altAnim = null) {
            var sb = new Storyboard();
            var isLarge = false;
            //process resizing
            if (newSize.HasValue) {
                var showScale = true;
                if (double.IsNaN(IM.Width) || double.IsNaN(IM.Height)) {
                    //skip animation when Width or Height is not set
                    IM.Width = newSize.Value.Width;
                    IM.Height = newSize.Value.Height;
                    showScale = false;
                }
                else if ((!altAnim.HasValue || altAnim.Value) &&
                         Math.Abs(newSize.Value.Width / IM.Width - 1d) > 0.5d &&
                         Math.Abs(newSize.Value.Height / IM.Height - 1d) > 0.5d) {
                    isLarge = true;
                    //for large images, use alternate animation to reduce stutter
                    var animOp = new DoubleAnimationUsingKeyFrames();
                    animOp.KeyFrames.Add(new EasingDoubleKeyFrame(0.01d, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100)), new CubicEase() { EasingMode = EasingMode.EaseIn }));
                    animOp.KeyFrames.Add(new LinearDoubleKeyFrame(0.01d, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100+20))));
                    animOp.KeyFrames.Add(new EasingDoubleKeyFrame(1d, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100*2+20)), new CubicEase() { EasingMode = EasingMode.EaseOut }));
                    Storyboard.SetTargetProperty(animOp, new PropertyPath(nameof(Opacity)));
                    var animW = new DoubleAnimationUsingKeyFrames();
                    animW.KeyFrames.Add(new DiscreteDoubleKeyFrame(newSize.Value.Width, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100+1))));
                    Storyboard.SetTargetProperty(animW, new PropertyPath(nameof(Width)));
                    var animH = new DoubleAnimationUsingKeyFrames();
                    animH.KeyFrames.Add(new DiscreteDoubleKeyFrame(newSize.Value.Height, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100+1))));
                    Storyboard.SetTargetProperty(animH, new PropertyPath(nameof(Height)));
                    sb.Children.Add(animOp);
                    sb.Children.Add(animW);
                    sb.Children.Add(animH);
                }
                else {
                    //for normal sized images
                    var animW = new DoubleAnimation(newSize.Value.Width, new Duration(TimeSpan.FromMilliseconds(ms)))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    var animH = new DoubleAnimation(newSize.Value.Height, new Duration(TimeSpan.FromMilliseconds(ms)))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    Storyboard.SetTargetProperty(animW, new PropertyPath(nameof(Width)));
                    Storyboard.SetTargetProperty(animH, new PropertyPath(nameof(Height)));
                    sb.Children.Add(animW);
                    sb.Children.Add(animH);
                }
                if (showScale) {
                    sb.Completed += (o1, e1) => BM.Show($"{IM.Scale:P1}");
                }
            }
            //process moving
            if (transPoint.HasValue) {
                transPoint = new Point(transPoint.Value.X.RoundToMultiplesOf(IM.TransformFromDevice.M11),
                                       transPoint.Value.Y.RoundToMultiplesOf(IM.TransformFromDevice.M22));
                DoubleAnimation animX, animY;
                if (isLarge) {
                    //for large images, move instantly when invisible
                    animX = new DoubleAnimation(transPoint.Value.X, new Duration(TimeSpan.Zero))
                    { BeginTime = TimeSpan.FromMilliseconds(100+1) };
                    animY = new DoubleAnimation(transPoint.Value.Y, new Duration(TimeSpan.Zero))
                    { BeginTime = TimeSpan.FromMilliseconds(100+1) };
                }
                else {
                    animX = new DoubleAnimation(transPoint.Value.X, new Duration(TimeSpan.FromMilliseconds(ms)))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    animY = new DoubleAnimation(transPoint.Value.Y, new Duration(TimeSpan.FromMilliseconds(ms)))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                }
                Storyboard.SetTargetProperty(animX, new PropertyPath("RenderTransform.Children[0].X"));
                Storyboard.SetTargetProperty(animY, new PropertyPath("RenderTransform.Children[0].Y"));
                sb.Children.Add(animX);
                sb.Children.Add(animY);
            }
            if (ms > 0) {
                //use Transforming property to indicate whether the animation is playing
                var animB = new BooleanAnimationUsingKeyFrames();
                animB.KeyFrames.Add(new DiscreteBooleanKeyFrame(true, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                animB.KeyFrames.Add(new DiscreteBooleanKeyFrame(false, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms))));
                Storyboard.SetTargetProperty(animB, new PropertyPath(nameof(IM.Transforming)));
                sb.Children.Add(animB);
            }

            if (sb.Children.Count > 0) {
                foreach (var child in sb.Children) Storyboard.SetTarget(child, IM);
                BeginStoryboard(sb, HandoffBehavior.Compose);
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
            }
        }

        private void CA_PreviewMouseMove(object sender, MouseEventArgs e) {
            if (!CA.IsMouseCaptured) return;
            transform(50, transPoint:
                new Point(existingTranslate.OffsetX + ((e.GetPosition(CA).X - mouseCapturePoint.X) * IM.Scale * 2d).RoundToMultiplesOf(IM.TransformFromDevice.M11),
                          existingTranslate.OffsetY + ((e.GetPosition(CA).Y - mouseCapturePoint.Y) * IM.Scale * 2d).RoundToMultiplesOf(IM.TransformFromDevice.M22)));
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
                        if (Setting.ViewerTransition == Setting.Transition.Random) {
                            var transVals = Enum.GetValues(typeof(Setting.Transition));
                            LastTransition = (Setting.Transition)transVals.GetValue(App.Random.Next(2, transVals.Length));
                        }
                        else LastTransition = Setting.ViewerTransition;

                        int multi = 1;
                        switch (Setting.ViewerTransitionSpeed) {
                            case Setting.TransitionSpeed.Medium:
                                multi = 2;
                                break;
                            case Setting.TransitionSpeed.Slow:
                                multi = 3;
                                break;
                        }
                        switch (LastTransition) {
                            case Setting.Transition.ZoomFadeBlur:
                                TransParams = new Tuple<TransParam, TransParam>(
                                    new TransParam(1 - 0.05 * increment, dur1: 200 * multi),
                                    new TransParam(dur1: 500 * multi));
                                break;

                            case Setting.Transition.Fade:
                                TransParams = new Tuple<TransParam, TransParam>(
                                    new TransParam(dur1: 200 * multi),
                                    new TransParam(dur1: 500 * multi));
                                break;

                            case Setting.Transition.HorizontalSwipe:
                                //bound to From, To and Duration respectively
                                TransParams = new Tuple<TransParam, TransParam>(
                                    new TransParam(0d, (IM_TT.X - IM.Width / 2d) * increment, 400 * multi),
                                    new TransParam(IM.Width / 2d * increment, 0d, 500 * multi));
                                break;
                            case Setting.Transition.None:
                                break;
                        }
                        if (LastTransition != Setting.Transition.None) {
                            IM.BeginStoryboard((Storyboard)IM.FindResource($"SB_Trans_{LastTransition}_Out"));
                            IM.AnimateBool(DpiImage.TransformingProperty, true, false, (int)TransParams.Item1.Duration1.TimeSpan.TotalMilliseconds);
                        }

                        //load next or previous image
                        Task.Run(() => {
                            App.MainWin.LoadPath(next, this);
                        });
                        return;
                    }

                    BM.Show("No more!");
                    break;
            }
        }

    }
}
