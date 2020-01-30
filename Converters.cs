using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ZipImageViewer
{
    public class RectConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return new Rect(0d, 0d, (double)values[0], (double)values[1]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class FlagToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var brush = Brushes.DimGray;
            if (value is FileFlags flags) {
                if (flags.HasFlag(FileFlags.Directory)) brush = Brushes.Goldenrod;
                else if (flags.HasFlag(FileFlags.Archive)) brush = Brushes.DarkRed;
                else if (flags.HasFlag(FileFlags.Image)) brush = Brushes.Transparent;
            }
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
