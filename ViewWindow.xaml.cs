using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ZipImageViewer
{

    public partial class ViewWindow : BorderlessWindow, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private KeyedCollection<string, ObjectInfo> objectList;

        public ObjectInfo ObjectInfo
        {
            get { return (ObjectInfo)GetValue(ObjectInfoProperty); }
            set { SetValue(ObjectInfoProperty, value); }
        }
        public static readonly DependencyProperty ObjectInfoProperty =
            Thumbnail.ObjectInfoProperty.AddOwner(typeof(ViewWindow), new PropertyMetadata(new PropertyChangedCallback((o, e) => {
                if (!(e.NewValue is ObjectInfo objInfo) || objInfo.ImageSources.Length == 0) return;
                var win = (ViewWindow)o;
                if (!win.IsLoaded)
                    //update directly without animations when window is not loaded yet (first time opening)
                    win.ViewImageSource = win.ObjectInfo.ImageSources[0];
                else if (win.IM.Transforming)
                    //update after transform end
                    DependencyPropertyDescriptor.FromProperty(DpiImage.TransformingProperty, typeof(DpiImage)).AddValueChanged(win.IM, updateViewImageSource);
                else
                    //update with "in" animations following the previous "out" animations
                    updateViewImageSource(win.IM, null);
            })));


        private static void updateViewImageSource(object obj, EventArgs e) {
            DependencyPropertyDescriptor.FromProperty(DpiImage.TransformingProperty, typeof(DpiImage)).RemoveValueChanged(obj, updateViewImageSource);
            var im = (DpiImage)obj;
            var win = (ViewWindow)GetWindow(im);
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


        public ObservablePair<DependencyProps, DependencyProps> TransParams {
            get { return (ObservablePair<DependencyProps, DependencyProps>)GetValue(TransParamsProperty); }
            set { SetValue(TransParamsProperty, value); }
        }
        public static readonly DependencyProperty TransParamsProperty =
            DependencyProperty.Register("TransParams", typeof(ObservablePair<DependencyProps, DependencyProps>), typeof(ViewWindow));


        private Setting.Transition LastTransition = Setting.Transition.None;

        //public Point CenterPoint =>
        //    new Point((CA.ActualWidth - IM.ActualWidth) / 2, (CA.ActualHeight - IM.ActualHeight) / 2);

        public ViewWindow(Window owner, KeyedCollection<string, ObjectInfo> objList)
        {
            objectList = objList;
            Owner = owner;
            Opacity = 0d;
            InitializeComponent();
        }

        private void ViewWindow_Loaded(object sender, RoutedEventArgs e) {
            scaleToCanvas(0); //needed for first time opening due to impossible to layout before loaded
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
                if (double.IsNaN(IM.Width) || double.IsNaN(IM.Height)) {
                    //skip animation when Width or Height is not set
                    IM.Width = newSize.Value.Width;
                    IM.Height = newSize.Value.Height;
                }
                else {
                    var rW = newSize.Value.Width / IM.Width;
                    var rH = newSize.Value.Height / IM.Height;
                    if (ms > 0 &&
                        (!altAnim.HasValue || altAnim.Value) &&
                        (rW < 0.5d || 2d < rW) && (rH < 0.5d || 2d < rH)) {
                        isLarge = true;
                        //for large images, use alternate animation to reduce stutter
                        var animOp = new DoubleAnimationUsingKeyFrames();
                        animOp.KeyFrames.Add(new EasingDoubleKeyFrame(0.01d, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150)), App.CE_EaseIn));
                        animOp.KeyFrames.Add(new LinearDoubleKeyFrame(0.01d, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150 + 20))));
                        animOp.KeyFrames.Add(new EasingDoubleKeyFrame(1d, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150 * 3 + 20)), App.CE_EaseOut));
                        Storyboard.SetTargetProperty(animOp, new PropertyPath(nameof(Opacity)));
                        var animW = new DoubleAnimationUsingKeyFrames();
                        animW.KeyFrames.Add(new DiscreteDoubleKeyFrame(newSize.Value.Width, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150 + 1))));
                        Storyboard.SetTargetProperty(animW, new PropertyPath(nameof(Width)));
                        var animH = new DoubleAnimationUsingKeyFrames();
                        animH.KeyFrames.Add(new DiscreteDoubleKeyFrame(newSize.Value.Height, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150 + 1))));
                        Storyboard.SetTargetProperty(animH, new PropertyPath(nameof(Height)));
                        sb.Children.Add(animOp);
                        sb.Children.Add(animW);
                        sb.Children.Add(animH);
                    }
                    else {
                        //for normal sized images
                        var animW = new DoubleAnimation(newSize.Value.Width, new Duration(TimeSpan.FromMilliseconds(ms)));
                        var animH = new DoubleAnimation(newSize.Value.Height, new Duration(TimeSpan.FromMilliseconds(ms)));
                        if (ms > 0) {
                            animW.EasingFunction = App.CE_EaseOut;
                            animH.EasingFunction = App.CE_EaseOut;
                        }
                        Storyboard.SetTargetProperty(animW, new PropertyPath(nameof(Width)));
                        Storyboard.SetTargetProperty(animH, new PropertyPath(nameof(Height)));
                        sb.Children.Add(animW);
                        sb.Children.Add(animH);
                    }
                    if (ms > 0)
                        BM.Show($"{newSize.Value.Width / IM.RealSize.Width:P1}");
                }
            }
            //process moving
            if (transPoint.HasValue) {
                transPoint = new Point(transPoint.Value.X.RoundToMultiplesOf(IM.DpiMultiplier.X),
                                       transPoint.Value.Y.RoundToMultiplesOf(IM.DpiMultiplier.Y));
                DoubleAnimation animX, animY;
                if (isLarge) {
                    //for large images, move instantly when invisible
                    animX = new DoubleAnimation(transPoint.Value.X, new Duration(TimeSpan.Zero))
                    { BeginTime = TimeSpan.FromMilliseconds(150 + 1) };
                    animY = new DoubleAnimation(transPoint.Value.Y, new Duration(TimeSpan.Zero))
                    { BeginTime = TimeSpan.FromMilliseconds(150 + 1) };
                }
                else {
                    animX = new DoubleAnimation(transPoint.Value.X, new Duration(TimeSpan.FromMilliseconds(ms)));
                    animY = new DoubleAnimation(transPoint.Value.Y, new Duration(TimeSpan.FromMilliseconds(ms)));
                    if (ms > 0) {
                        animX.EasingFunction = App.CE_EaseOut;
                        animY.EasingFunction = App.CE_EaseOut;
                    }
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
            var uniSize = Helpers.UniformScaleDown(IM.RealSize, new Size(CA.ActualWidth, CA.ActualHeight));
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
                new Point(existingTranslate.OffsetX + ((e.GetPosition(CA).X - mouseCapturePoint.X) * IM.Scale * 2d).RoundToMultiplesOf(IM.DpiMultiplier.X),
                          existingTranslate.OffsetY + ((e.GetPosition(CA).Y - mouseCapturePoint.Y) * IM.Scale * 2d).RoundToMultiplesOf(IM.DpiMultiplier.Y)));
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
                    //get direction for the next items.
                    //also used to determine direction for some animations.
                    var increment = e.Key == Key.Left ? -1 : 1;
                    //get index of the next item
                    var i = objectList.IndexOf(objectList[ObjectInfo.VirtualPath]) + increment;
                    while (i > -1 && i < objectList.Count) {
                        var next = objectList[i];
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
                                TransParams = new ObservablePair<DependencyProps, DependencyProps>(
                                    new DependencyProps(1 - 0.05 * increment, dur1: 200 * multi),
                                    new DependencyProps(dur1: 500 * multi));
                                break;

                            case Setting.Transition.Fade:
                                TransParams = new ObservablePair<DependencyProps, DependencyProps>(
                                    new DependencyProps(dur1: 200 * multi),
                                    new DependencyProps(dur1: 500 * multi));
                                break;

                            case Setting.Transition.HorizontalSwipe:
                                //bound to From, To and Duration respectively
                                TransParams = new ObservablePair<DependencyProps, DependencyProps>(
                                    new DependencyProps(0d, (IM_TT.X - IM.Width / 2d) * increment, 400 * multi),
                                    new DependencyProps(IM.Width / 2d * increment, 0d, 500 * multi));
                                break;
                            case Setting.Transition.None:
                                break;
                        }
                        if (LastTransition != Setting.Transition.None) {
                            IM.BeginStoryboard((Storyboard)IM.FindResource($"SB_Trans_{LastTransition}_Out"));
                            IM.AnimateBool(DpiImage.TransformingProperty, true, false, (int)TransParams.Item1.Dur1.TimeSpan.TotalMilliseconds);
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

        private void ViewWin_Unloaded(object sender, RoutedEventArgs e) {
            objectList = null;
            ViewImageSource = null;
            ObjectInfo.ImageSources = null;
            ObjectInfo = null;
            IM.Source = null;
            IM = null;
        }
    }
}
