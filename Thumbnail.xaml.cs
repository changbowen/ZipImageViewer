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
        public ImageSource Source
        {
            get { return (ImageSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(ImageSource), typeof(Thumbnail), new PropertyMetadata(null));


        public string  FileName
        {
            get { return (string )GetValue(FileNameProperty); }
            set { SetValue(FileNameProperty, value); }
        }
        public static readonly DependencyProperty FileNameProperty =
            DependencyProperty.Register("FileName", typeof(string ), typeof(Thumbnail), new PropertyMetadata(null));

        public string FullPath { get; set; }


        public Thumbnail() {
            InitializeComponent();
        }

        public Thumbnail(ImageSource source, string fileName = null) {
            FileName = fileName;
            Source = source;

            InitializeComponent();
        }

        public Thumbnail(string filePath) {
            FullPath = filePath;
            FileName = Path.GetFileName(filePath);
            Source = new ImageSourceConverter().ConvertFromString(filePath) as ImageSource;

            InitializeComponent();
        }
    }
}
