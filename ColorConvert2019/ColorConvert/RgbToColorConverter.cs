using System;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;

namespace ColorConvert
{
    public class RgbToColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // object 型の配列にはバインドした順でデータが入ってくる
            byte red = (byte)(double)(values[0]);
            byte green = (byte)(double)(values[1]);
            byte blue = (byte)(double)(values[2]);

            return Color.FromRgb(red, green, blue);
        }

        //-----------------------------------------------------------------------------------------------
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
