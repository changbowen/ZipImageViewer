using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ZipImageViewer
{
    public class BorderlessWindow : Window
    {
        public bool ButtonCloseVisible {
            get { return (bool)GetValue(ButtonCloseVisibleProperty); }
            set { SetValue(ButtonCloseVisibleProperty, value); }
        }
        public static readonly DependencyProperty ButtonCloseVisibleProperty =
            DependencyProperty.Register("ButtonCloseVisible", typeof(bool), typeof(BorderlessWindow), new PropertyMetadata(true));


        public bool ButtonMaxVisible {
            get { return (bool)GetValue(ButtonMaxVisibleProperty); }
            set { SetValue(ButtonMaxVisibleProperty, value); }
        }
        public static readonly DependencyProperty ButtonMaxVisibleProperty =
            DependencyProperty.Register("ButtonMaxVisible", typeof(bool), typeof(BorderlessWindow), new PropertyMetadata(true));


        public bool ButtonMinVisible {
            get { return (bool)GetValue(ButtonMinVisibleProperty); }
            set { SetValue(ButtonMinVisibleProperty, value); }
        }
        public static readonly DependencyProperty ButtonMinVisibleProperty =
            DependencyProperty.Register("ButtonMinVisible", typeof(bool), typeof(BorderlessWindow), new PropertyMetadata(true));


        public BorderlessWindow() {
            //need below line for styles to apply to derived window classes.
            //otherwise need to move Generic.xaml to Themes folder and add this in static BorderlessWindow() { }:
            //DefaultStyleKeyProperty.OverrideMetadata(typeof(BorderlessWindow), new FrameworkPropertyMetadata(typeof(BorderlessWindow)));

            SetResourceReference(StyleProperty, typeof(BorderlessWindow));
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            //system button click handlers
            if (GetTemplateChild("minimizeButton") is Button minimizeButton) minimizeButton.Click += MinimizeClick;
            if (GetTemplateChild("restoreButton") is Button restoreButton) restoreButton.Click += RestoreClick;
            if (GetTemplateChild("closeButton") is Button closeButton) closeButton.Click += CloseClick;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e) {
            base.OnMouseDown(e);
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        protected void MinimizeClick(object sender, RoutedEventArgs e) {
            WindowState = WindowState.Minimized;
        }

        protected void RestoreClick(object sender, RoutedEventArgs e) {
            WindowState = (WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
        }

        protected void CloseClick(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
