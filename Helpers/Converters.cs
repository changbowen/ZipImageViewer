using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ZipImageViewer
{
    public class EncryptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (!(value is string input)) return null;
            if (Setting.EncryptPasswords == null || Setting.EncryptPasswords == false) return value;

            if (((string)parameter)?.ToLowerInvariant() == "encrypt")
                return EncryptionHelper.TryEncrypt(input).Output;
            else
                return EncryptionHelper.TryDecrypt(input).Output;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (!(value is string input)) return null;
            if (Setting.EncryptPasswords == null || Setting.EncryptPasswords == false) return value;

            if (((string)parameter)?.ToLowerInvariant() == "encrypt")
                return EncryptionHelper.TryDecrypt(input).Output;
            else
                return EncryptionHelper.TryEncrypt(input).Output;
        }
    }

    /// <summary>
    /// Convert null to false. Return non-null values as-is.
    /// </summary>
    public class NullableBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value ?? false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return value;
        }
    }

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

    public class CustomCmdArgsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            if (values.Length < 3) return Binding.DoNothing;
            var path = values[0] as string;
            var args = values[1] as string;
            var objInfo = values[2] as ObjectInfo;
            var realArgs = args;
            if (!string.IsNullOrWhiteSpace(args) && objInfo != null)
                realArgs = Helpers.CustomCmdArgsReplace(args, objInfo);
            return $@"{path} {realArgs}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class CenterParentConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            if (values.Length < 3) return Binding.DoNothing;
            if (values[0] is double ownerXY && values[1] is double ownerWH && values[2] is double selfWH) {
                return ownerXY + (ownerWH - selfWH) / 2;
            }
            else return Binding.DoNothing;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class FileSizeHumanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (!(value is long l)) return Binding.DoNothing;
            return Helpers.BytesToString(l);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class DrawingSizeStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value == null) return Binding.DoNothing;
            var sizeInt = (System.Drawing.Size)value;
            return sizeInt.Width + @" x " + sizeInt.Height;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class ResourceConverter : IMultiValueConverter
    {
        /// <summary>
        /// value[0] converted to string will be used to get resource key.
        /// parameter is the string format will be used to format resource key.
        /// value[1] is optional and will be used to get resource from if specified.
        /// </summary>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
            if (values == null || values.Length == 0) return Binding.DoNothing;
            var key = values[0];
            if (parameter is string param)
                key = string.Format(param, key);
            object res;
            if (values.Length > 1 && values[1] is FrameworkElement ele)
                res = ele.Resources[key];
            else
                res = Application.Current.Resources[key];
            if (res == null) return Binding.DoNothing;
            return res;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class ExtractIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            ImageSource source = null;
            if (value is string path) {
                string realPath = null;
                if (path.PathIsFile().HasValue)
                    realPath = path;
                else {
                    foreach (var dir in Environment.GetEnvironmentVariable(@"PATH").Split(Path.PathSeparator)) {
                        var c = Path.Combine(dir, path);
                        if (c.PathIsFile() == null) continue;
                        realPath = c;
                        break;
                    }
                }

                if (!string.IsNullOrWhiteSpace(realPath)) {
                    source = NativeHelpers.GetIcon(realPath, false, realPath.PathIsFile() == false);
                    //using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(realPath)) {
                    //    source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    //        icon.Handle,
                    //        Int32Rect.Empty,
                    //        BitmapSizeOptions.FromEmptyOptions());
                    //}
                }
            }
            
            if (source == null)
                source = Helpers.GetFaIcon(FontAwesome5.EFontAwesomeIcon.Solid_ExternalLinkAlt);
            return source;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
