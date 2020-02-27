using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ZipImageViewer
{
    public partial class BlockWindow : RoundedWindow
    {
        public int Percentage {
            get { return (int)GetValue(PercentageProperty); }
            set { SetValue(PercentageProperty, value); }
        }
        public static readonly DependencyProperty PercentageProperty =
            DependencyProperty.Register("Percentage", typeof(int), typeof(BlockWindow), new PropertyMetadata(-1));

        public string MessageTitle {
            get { return (string)GetValue(MessageTitleProperty); }
            set { SetValue(MessageTitleProperty, value); }
        }
        public static readonly DependencyProperty MessageTitleProperty =
            DependencyProperty.Register("MessageTitle", typeof(string), typeof(BlockWindow), new PropertyMetadata(""));

        public string MessageBody {
            get { return (string)GetValue(MessageBodyProperty); }
            set { SetValue(MessageBodyProperty, value); }
        }
        public static readonly DependencyProperty MessageBodyProperty =
            DependencyProperty.Register("MessageBody", typeof(string), typeof(BlockWindow), new PropertyMetadata("Please wait..."));


        /// <summary>
        /// Need to set the CancellationTokenSource to null in Work for window to close properly.
        /// </summary>
        public Action Work { get; set; }
        internal CancellationTokenSource tknSrc_Work;
        internal readonly object lock_Work = new object();

        public BlockWindow(Window owner) {
            Owner = owner;
            InitializeComponent();
        }

        private void BlockWin_Loaded(object sender, RoutedEventArgs e) {
            foreach (Window win in Owner.OwnedWindows) {
                if (win == this) continue;
                win.IsEnabled = false;
                win.IsHitTestVisible = false;
            }

            var thrd = new Thread(new ThreadStart(Work)) { IsBackground = true };
            thrd.Start();
        }

        private async void BlockWin_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            tknSrc_Work?.Cancel();
            while (tknSrc_Work != null) {
                await Task.Delay(200);
            }

            foreach (Window win in Owner.OwnedWindows) {
                if (win == this) continue;
                win.IsEnabled = true;
                win.IsHitTestVisible = true;
            }
        }
    }
}
