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
        public Visibility ButtonCloseVisibility {
            get { return (Visibility)GetValue(ButtonCloseVisibilityProperty); }
            set { SetValue(ButtonCloseVisibilityProperty, value); }
        }
        public static readonly DependencyProperty ButtonCloseVisibilityProperty =
            DependencyProperty.Register("ButtonCloseVisibility", typeof(Visibility), typeof(BorderlessWindow), new PropertyMetadata(Visibility.Visible));


        public Visibility ButtonMinVisibility {
            get { return (Visibility)GetValue(ButtonMinVisibilityProperty); }
            set { SetValue(ButtonMinVisibilityProperty, value); }
        }
        public static readonly DependencyProperty ButtonMinVisibilityProperty =
            DependencyProperty.Register("ButtonMinVisibility", typeof(Visibility), typeof(BorderlessWindow), new PropertyMetadata(Visibility.Visible));


        public Visibility ButtonMaxVisibility {
            get { return (Visibility)GetValue(ButtonMaxVisibilityProperty); }
            set { SetValue(ButtonMaxVisibilityProperty, value); }
        }
        public static readonly DependencyProperty ButtonMaxVisibilityProperty =
            DependencyProperty.Register("ButtonMaxVisibility", typeof(Visibility), typeof(BorderlessWindow), new PropertyMetadata(Visibility.Visible));


        public Visibility TitleVisibility {
            get { return (Visibility)GetValue(TitleVisibilityProperty); }
            set { SetValue(TitleVisibilityProperty, value); }
        }
        public static readonly DependencyProperty TitleVisibilityProperty =
            DependencyProperty.Register("TitleVisibility", typeof(Visibility), typeof(BorderlessWindow), new PropertyMetadata(Visibility.Visible));


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
            if (e.Source is Window &&
                e.ChangedButton == MouseButton.Left &&
                e.ButtonState == MouseButtonState.Pressed) {
                DragMove();
            }
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
