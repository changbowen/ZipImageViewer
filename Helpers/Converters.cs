using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
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

    //public class RectConverter : IMultiValueConverter
    //{
    //    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    //    {
    //        return new Rect(0d, 0d, (double)values[0], (double)values[1]);
    //    }

    //    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    //    {
    //        throw new NotSupportedException();
    //    }
    //}

    public class RectConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            //parameter is the clipped width to preserve
            var offset = 0d; var thickness = 20d;
            if (parameter is string paraStr) {
                var paraAry = paraStr.Split(',', ' ');
                if (paraAry.Length > 0) offset = double.Parse(paraAry[0]);
                if (paraAry.Length > 1) thickness = double.Parse(paraAry[1]);
            }
            var width = (double)values[0];
            var height = (double)values[1];
            return new Rect(new Point(-offset, -offset), new Point(width + offset - thickness, height + offset - thickness));//20是两边空隙总和
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
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

    public class MathMultiplyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (!(value is double)) value = System.Convert.ToDouble(value);
            if (!(parameter is double)) parameter = System.Convert.ToDouble(parameter);
            return (double)value * (double)parameter;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class ThicknessMultiplyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var val = System.Convert.ToDouble(value);
            var paras = (parameter as string)?.Split(',', ' ');
            if (paras?.Length == 4)
                return new Thickness(val * double.Parse(paras[0]),
                                     val * double.Parse(paras[1]),
                                     val * double.Parse(paras[2]),
                                     val * double.Parse(paras[3]));
            else
                return new Thickness(val);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    //public class ContextMenuTemplateSelector : DataTemplateSelector
    //{
    //    public override DataTemplate SelectTemplate(object item, DependencyObject container) {
    //        if (item == CollectionView.NewItemPlaceholder)
    //            return (container as FrameworkElement).TryFindResource("EmptyDataTemplate") as DataTemplate;
    //        else
    //            return (container as FrameworkElement).TryFindResource("ContextMenuDataTemplate") as DataTemplate;
    //    }
    //}


    public class CustomCmdArgsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            var str = values[0] as string;
            var tn = values[1] as Thumbnail;
            if (string.IsNullOrWhiteSpace(str) || tn == null || tn.ObjectInfo == null)
                return str;
            return Helpers.CustomCmdArgsReplace(str, tn.ObjectInfo);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
