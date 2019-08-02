﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

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
}
