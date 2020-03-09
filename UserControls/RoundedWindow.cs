using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ZipImageViewer
{
    public class RoundedWindow : Window
    {
        public enum CloseBehaviors { Close, Hide, FadeOutAndHide, FadeOutAndClose }

        #region Properties
        /// <summary>
        /// Get or set the background of the rounded rectangle.
        /// </summary>
        public new Brush Background {
            get { return (Brush)GetValue(BackgroundProperty); }
            set { SetValue(BackgroundProperty, value); }
        }
        public static readonly new DependencyProperty BackgroundProperty =
            DependencyProperty.Register("Background", typeof(Brush), typeof(RoundedWindow), new PropertyMetadata(new SolidColorBrush(Color.FromRgb(255, 247, 197))));


        public bool ButtonCloseVisible {
            get { return (bool)GetValue(ButtonCloseVisibleProperty); }
            set { SetValue(ButtonCloseVisibleProperty, value); }
        }
        public static readonly DependencyProperty ButtonCloseVisibleProperty =
            DependencyProperty.Register("ButtonCloseVisible", typeof(bool), typeof(RoundedWindow), new PropertyMetadata(true));


        public bool ButtonMaxVisible {
            get { return (bool)GetValue(ButtonMaxVisibleProperty); }
            set { SetValue(ButtonMaxVisibleProperty, value); }
        }
        public static readonly DependencyProperty ButtonMaxVisibleProperty =
            DependencyProperty.Register("ButtonMaxVisible", typeof(bool), typeof(RoundedWindow), new PropertyMetadata(false));


        public bool ButtonMinVisible {
            get { return (bool)GetValue(ButtonMinVisibleProperty); }
            set { SetValue(ButtonMinVisibleProperty, value); }
        }
        public static readonly DependencyProperty ButtonMinVisibleProperty =
            DependencyProperty.Register("ButtonMinVisible", typeof(bool), typeof(RoundedWindow), new PropertyMetadata(false));


        public CloseBehaviors CloseBehavior {
            get { return (CloseBehaviors)GetValue(CloseBehaviorProperty); }
            set { SetValue(CloseBehaviorProperty, value); }
        }
        public static readonly DependencyProperty CloseBehaviorProperty =
            DependencyProperty.Register("CloseBehavior", typeof(CloseBehaviors), typeof(RoundedWindow), new PropertyMetadata(CloseBehaviors.FadeOutAndClose));

        /// <summary>
        /// If set to false, keyboard focus will be set to the owner when the window gets focus.
        /// </summary>
        public new bool Focusable {
            get { return (bool)GetValue(FocusableProperty); }
            set { SetValue(FocusableProperty, value); }
        }
        public static readonly new DependencyProperty FocusableProperty =
            DependencyProperty.Register("Focusable", typeof(bool), typeof(RoundedWindow), new PropertyMetadata(true));

        /// <summary>
        /// This maps to the RenderTransformOrigin property of BackgroundGrid.
        /// </summary>
        public Point RenderTransformOrigin_BG {
            get { return (Point)GetValue(RenderTransformOrigin_BGProperty); }
            set { SetValue(RenderTransformOrigin_BGProperty, value); }
        }
        public static readonly DependencyProperty RenderTransformOrigin_BGProperty =
            DependencyProperty.Register("RenderTransformOrigin_BG", typeof(Point), typeof(RoundedWindow), new PropertyMetadata(new Point(0.5d, 0.5d)));


        public double CornerRadius {
            get { return (double)GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register("CornerRadius", typeof(double), typeof(RoundedWindow), new PropertyMetadata(8d));

        public bool MenuMode
        {
            get { return (bool)GetValue(MenuModeProperty); }
            set { SetValue(MenuModeProperty, value); }
        }
        public static readonly DependencyProperty MenuModeProperty =
            DependencyProperty.Register("MenuMode", typeof(bool), typeof(RoundedWindow), new PropertyMetadata(false));

        #endregion


        public static readonly RoutedEvent FadingInEvent = EventManager.RegisterRoutedEvent("FadingIn", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(RoundedWindow));
        public event RoutedEventHandler FadingIn
        {
            add { AddHandler(FadingInEvent, value); }
            remove { RemoveHandler(FadingInEvent, value); }
        }

        
        public static readonly RoutedEvent FadingOutEvent = EventManager.RegisterRoutedEvent("FadingOut", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(RoundedWindow));
        public event RoutedEventHandler FadingOut
        {
            add { AddHandler(FadingOutEvent, value); }
            remove { RemoveHandler(FadingOutEvent, value); }
        }

        public static readonly RoutedEvent FadedOutEvent = EventManager.RegisterRoutedEvent("FadedOut", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(RoundedWindow));
        public event RoutedEventHandler FadedOut {
            add { AddHandler(FadedOutEvent, value); }
            remove { RemoveHandler(FadedOutEvent, value); }
        }

        public bool? IsModal {
            get {
                var result = typeof(Window).GetField("_showingAsDialog", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(this);
                return (bool?)result;
            }
        }

        /// <summary>
        /// This is a grid on which the scaling animations are applied.
        /// </summary>
        internal Grid BackgroundGrid { get; set; }
        public Window ParentWindow { get; set; }
        private bool? dialogResult;

        public RoundedWindow() {
            SetResourceReference(StyleProperty, typeof(RoundedWindow));

            AllowsTransparency = true;
            WindowStyle = WindowStyle.None;
            base.Background = null;
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            BackgroundGrid = (Grid)GetTemplateChild("Grid_Main");

            if (GetTemplateChild("minimizeButton") is Button minimizeButton) minimizeButton.Click += MinimizeClick;
            if (GetTemplateChild("restoreButton") is Button restoreButton) restoreButton.Click += RestoreClick;
            if (GetTemplateChild("closeButton") is Button closeButton) closeButton.Click += CloseClick;

            if (!Focusable && Owner != null) {
                PreviewGotKeyboardFocus += (object sender, KeyboardFocusChangedEventArgs e) => {
                    e.Handled = true;
                    if (sender is Window o && o != this) o.Focus();
                };
            }
        }

        #region Click Events
        protected void MinimizeClick(object sender, RoutedEventArgs e) {
            WindowState = WindowState.Minimized;
        }

        protected void RestoreClick(object sender, RoutedEventArgs e) {
            WindowState = (WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
        }

        protected void CloseClick(object sender, RoutedEventArgs e) {
            //make sure the scale animation ends at the top right corner when close is clicked.
            RenderTransformOrigin_BG = new Point(1d, 0d);
            Close();
        }
        #endregion

        private void ParentWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) {//right click moves the window
                //unsubParentMouseDown();
                Close();
                e.Handled = true;
            }
        }

        private readonly HashSet<IntPtr> subscribedOwners = new HashSet<IntPtr>();
        private void subParentMouseDown() {
            if (ParentWindow == null) return;
            var hwnd = new WindowInteropHelper(ParentWindow).Handle;
            if (hwnd == null || hwnd == IntPtr.Zero) return;
            if (subscribedOwners.Contains(hwnd)) return;
            ParentWindow.PreviewMouseDown += ParentWindow_PreviewMouseDown;
            subscribedOwners.Add(hwnd);
        }
        private void unsubParentMouseDown() {
            if (ParentWindow == null) return;
            var hwnd = new WindowInteropHelper(ParentWindow).Handle;
            if (hwnd == null || hwnd == IntPtr.Zero) return;
            ParentWindow.PreviewMouseDown -= ParentWindow_PreviewMouseDown;
            subscribedOwners.Remove(hwnd);
        }

        public new void Show() {
            if (MenuMode) {
                if (ParentWindow == null)
                    throw new ArgumentNullException($"{nameof(ParentWindow)} cannot be null when {nameof(MenuMode)} is true.");
                Topmost = true;
                subParentMouseDown();
            }
            base.Show();
        }

        protected override void OnClosing(CancelEventArgs e) {
            unsubParentMouseDown();

            switch (CloseBehavior) {
                case CloseBehaviors.Close:
                    //really closing
                    ParentWindow = null;
                    base.OnClosing(e);
                    break;
                case CloseBehaviors.FadeOutAndClose:
                    dialogResult = DialogResult;
                    e.Cancel = true;
                    FadeOut(true);
                    break;
                case CloseBehaviors.FadeOutAndHide:
                    e.Cancel = true;
                    FadeOut();
                    break;
                case CloseBehaviors.Hide:
                    e.Cancel = true;
                    Hide();
                    break;
            }
        }

        /// <summary>
        /// Fade in the window at mouse position. Or the specified Left and Top position.
        /// If the window is already faded in, move the window to the new position. In this case the FadingInEvent will not be raised.
        /// </summary>
        public void FadeIn(double left = double.NaN, double top = double.NaN) {
            if (!IsLoaded) {
                Opacity = 0d;
                Show();//how to call something similar to initializecomponent?
            }

            //compute mouse position or set to existing values
            Point newpos, realpos = default;
            var mon = NativeHelpers.GetMonitorFromWindow(this);
            double currscrnW = mon.Right;
            double currscrnH = mon.Bottom;
            if (WindowStartupLocation == WindowStartupLocation.Manual) {
                if (left.Equals(double.NaN) || top.Equals(double.NaN)) {
                    //get the physical pixel-based position.
                    newpos = PointToScreen(Mouse.GetPosition(this));
                    //convert to the actual position considering the DPI settings etc.
                    realpos = PresentationSource.FromVisual(this).CompositionTarget.TransformFromDevice.Transform(newpos);

                    //make sure the window is displayed inside the screens.
                    double originX, originY;
                    if (currscrnW - realpos.X > ActualWidth)
                        originX = 0d;
                    else {
                        originX = 1d;
                        realpos.X -= ActualWidth;
                    }
                    if (currscrnH - realpos.Y > ActualHeight)
                        originY = 0d;
                    else {
                        originY = 1d;
                        realpos.Y -= ActualHeight;
                    }
                    RenderTransformOrigin_BG = new Point(originX, originY);
                }
                else {
                    //make sure the window is displayed inside the screens.
                    if (left < 0d) left = 0d;
                    if (top < 0d) top = 0d;
                    if (left + ActualWidth > currscrnW)
                        left = currscrnW - ActualWidth;
                    if (top + ActualHeight > currscrnH)
                        top = currscrnH - ActualHeight;
                    realpos = new Point(left, top);
                }
            }

            if (Opacity == 0) {
                if (realpos != default) {
                    Left = realpos.X;
                    Top = realpos.Y;
                }
                Show();
                RaiseEvent(new RoutedEventArgs(FadingInEvent));
            }
            else {
                //move window to the new cursor location
                var easefunc = new CubicEase() { EasingMode = EasingMode.EaseInOut };
                var anim_move_x = new DoubleAnimation(realpos.X, new Duration(new TimeSpan(0, 0, 0, 0, 300)), FillBehavior.Stop) { EasingFunction = easefunc };
                var anim_move_y = new DoubleAnimation(realpos.Y, new Duration(new TimeSpan(0, 0, 0, 0, 300)), FillBehavior.Stop) { EasingFunction = easefunc };
                BeginAnimation(LeftProperty, anim_move_x) ;
                BeginAnimation(TopProperty, anim_move_y);
            }
        }
        /// <summary>
        /// Fade out the window. This ignores the CloseBehavior setting.
        /// </summary>
        /// <param name="closeafterfade">Set to true to close the window after. Otherwise it is only hidden.</param>
        public async Task FadeOut(bool closeafterfade = false) {
            if (!IsLoaded) return;//without this it will crash at the below line.
            RaiseEvent(new RoutedEventArgs(FadingOutEvent));
            //need to be longer than the fading animation otherwise the window will flash when Show() is called.
            await Task.Delay(250);
            if (closeafterfade) {
                CloseBehavior = CloseBehaviors.Close;
                if (IsModal == true)
                    DialogResult = dialogResult;
                Close();
            }
            else Hide();
            RaiseEvent(new RoutedEventArgs(FadedOutEvent));
        }

        protected override void OnMouseDown(MouseButtonEventArgs e) {
            base.OnMouseDown(e);
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed) DragMove();
        }
    }
}
