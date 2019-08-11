using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZipImageViewer
{
//    public class FileNameConverter : IValueConverter {
//        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
//            if (value is string path)
//                return Path.GetFileName(path);
//            return value;
//        }
//
//        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
//            throw new NotSupportedException();
//        }
//    }

    public partial class Thumbnail : UserControl
    {
        public ImageInfo ImageInfo
        {
            get { return (ImageInfo)GetValue(ImageInfoProperty); }
            set { SetValue(ImageInfoProperty, value); }
        }
        public static readonly DependencyProperty ImageInfoProperty =
            DependencyProperty.Register("ImageInfo", typeof(ImageInfo), typeof(Thumbnail), new PropertyMetadata(null));


        public Thumbnail() {
            InitializeComponent();
        }


//        public Thumbnail(string realPath) {
//            RealPath = realPath;
//
//            DisplayName = Path.GetFileName(filePath);
//            Source = new ImageSourceConverter().ConvertFromString(filePath) as ImageSource;
//
//            InitializeComponent();
//        }
    }
}
