using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Timer = System.Timers.Timer;

namespace ZipImageViewer
{
    public partial class BubbleMessage : UserControl
    {
        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(BubbleMessage), new PropertyMetadata(null));

        private Timer timer;

        public BubbleMessage() {
            Opacity = 0d;
            timer = new Timer(2000) {AutoReset = false};
            timer.Elapsed += (s, e) =>
                Dispatcher.Invoke(() => BeginStoryboard((Storyboard) FindResource("SB_FadeOut")));

            InitializeComponent();
        }

        public void Show(string message) {
            TB.Text = message;
            if (timer.Enabled) {
                timer.Stop();
                timer.Start();
            }
            else {
                BeginStoryboard((Storyboard)FindResource("SB_FadeIn"));
                timer.Start();
            }
        }


    }
}
