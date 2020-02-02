using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ZipImageViewer
{
    public class RectConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            //if (values[0] == DependencyProperty.UnsetValue) values[0] = 0d;
            //if (values[1] == DependencyProperty.UnsetValue) values[1] = 0d;
            return new Rect(0d, 0d, (double)values[0], (double)values[1]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class FlagToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is FileFlags flags) {
                if (flags.HasFlag(FileFlags.Directory)) return FontAwesome5.EFontAwesomeIcon.Regular_Folder;
                else if (flags.HasFlag(FileFlags.Image)) return FontAwesome5.EFontAwesomeIcon.Regular_FileImage;
                else if (flags.HasFlag(FileFlags.Archive)) return FontAwesome5.EFontAwesomeIcon.Regular_FileArchive;
                else return FontAwesome5.EFontAwesomeIcon.Regular_QuestionCircle;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
