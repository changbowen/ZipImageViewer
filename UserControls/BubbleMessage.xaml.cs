using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
            DependencyProperty.Register("Message", typeof(string), typeof(BubbleMessage), new PropertyMetadata(""));


        public BubbleMessage() {
            Opacity = 0d;
            InitializeComponent();
        }

        public void Show(string message) {
            Message = message;
            BeginStoryboard((Storyboard)FindResource("SB_FadeInThenOut"));
        }

    }
}
