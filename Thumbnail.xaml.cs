using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ZipImageViewer
{
    public partial class Thumbnail : UserControl
    {
        public object FilePath
        {
            get { return GetValue(FilePathProperty); }
            set { SetValue(FilePathProperty, value); }
        }
        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register("FilePath", typeof(object), typeof(Thumbnail), new PropertyMetadata(null));


        public Thumbnail(object filepath)
        {
            FilePath = filepath;
            InitializeComponent();
        }
    }
}
