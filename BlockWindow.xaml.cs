using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static ZipImageViewer.Helpers;

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
            DependencyProperty.Register("MessageBody", typeof(string), typeof(BlockWindow), new PropertyMetadata(GetRes("msg_PleaseWait")));


        /// <summary>
        /// Need to set the CancellationTokenSource to null in Work for window to close properly.
        /// </summary>
        public Action Work { get; set; }
        public bool AutoClose { get; private set; } = false;
        internal CancellationTokenSource tknSrc_Work;
        internal readonly object lock_Work = new object();

        /// <summary>
        /// If set, <paramref name="owner"/> and its owned windows will be disabled until BlockWindow is closed.
        /// </summary>
        public BlockWindow(Window owner = null, bool autoClose = false) {
            InitializeComponent();

            Owner = owner;
            AutoClose = autoClose;
            ButtonCloseVisible = !autoClose;
        }

        private void BlockWin_Loaded(object sender, RoutedEventArgs e) {
            setOwnerState(false);

            var threadStart = new ThreadStart(Work);
            if (AutoClose) threadStart += () => Dispatcher.Invoke(Close);
            var thrd = new Thread(threadStart) { IsBackground = true };
            thrd.Start();
        }

        private async void BlockWin_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            tknSrc_Work?.Cancel();
            while (tknSrc_Work != null) {
                await Task.Delay(200);
            }

            setOwnerState(true);
        }

        private void setOwnerState(bool enable) {
            if (Owner == null) return;
            foreach (Window win in Owner.OwnedWindows) {
                if (win == this) continue;
                win.IsEnabled = enable;
                win.IsHitTestVisible = enable;
            }
            Owner.IsEnabled = enable;
            Owner.IsHitTestVisible = enable;
        }
    }
}
