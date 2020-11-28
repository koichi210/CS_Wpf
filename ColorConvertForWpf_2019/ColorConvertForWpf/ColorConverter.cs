using System;
using System.Windows.Data;
using System.Windows.Media;
using System.Globalization;

namespace ColorConvertForWpf
{
    public class ColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // object 型の配列にはバインドした順でデータが入ってくる
            byte R = (byte)(double)(values[0]);
            byte G = (byte)(double)(values[1]);
            byte B = (byte)(double)(values[2]);

            return Color.FromRgb(R, G, B);
            //return Color.FromRgb((byte)(double)(values[0]), (byte)(double)(values[1]), (byte)(double)(values[2]));
        }

        //-----------------------------------------------------------------------------------------------
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
