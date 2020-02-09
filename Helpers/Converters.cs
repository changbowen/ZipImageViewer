using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ZipImageViewer
{
    public class CenterConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            if (values == null || values.Length < 2)
                throw new ArgumentException("Two double values need to be passed in this order -> totalWidth, width", nameof(values));

            var totalWidth = (double)values[0];
            var width = (double)values[1];
            return (totalWidth - width) / 2;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

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

    //public class ImageSourceFallbackConverter : IValueConverter
    //{
    //    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
    //        switch (value) {
    //            case List<ImageSource> imgSources:
    //                if (imgSources.Count > 0) return imgSources[0];
    //                break;
    //        }
    //        return FontAwesome5.ImageAwesome.CreateImageSource(
    //            FontAwesome5.EFontAwesomeIcon.Solid_Meh,
    //            new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)));
    //    }

    //    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
    //        throw new NotImplementedException();
    //    }
    //}

    //public class MathAddConverter : IValueConverter
    //{
    //    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
    //        if (!(value is double)) value = System.Convert.ToDouble(value);
    //        if (!(parameter is double)) parameter = System.Convert.ToDouble(parameter);
    //        return (double)value + (double)parameter;
    //    }

    //    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
    //        throw new NotImplementedException();
    //    }
    //}
}
