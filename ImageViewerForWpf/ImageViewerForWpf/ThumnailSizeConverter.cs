using System;
using System.Collections.Generic;
using System.Windows.Data;

namespace ImageViewerForWpf
{
    public class ThumnailSizeNameConverter : IValueConverter
    {
        // Dictionaryを使用せずswitchを使用するときに使用
        // private readonly string m_ThumnailSizeStrS = "Small";
        // private readonly string m_ThumnailSizeStrM = "Middle";
        // private readonly string m_ThumnailSizeStrL = "Large";

        Dictionary<int, string> m_SizeName = new Dictionary<int, string>()
        { {1, "Small "},
          {2, "Middle"},
          {3, "Large "}
        };

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            //型変換の手法はいくつかある
            //int value1 = System.Convert.ToInt32(value);
            //int value2 = Int32.Parse(value.ToString());
            //int value3 = 0;
            //Int32.TryParse(value.ToString(), out value3);

            Int32 Idx = Int32.Parse(value.ToString());
            // TODO：XamlのSliderThumbnail値から範囲値を取得したい
            if (Idx < 1 || 3 < Idx )
            {
                return null;
            }

            return m_SizeName[Idx];
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }

    public class ThumnailSizeWidthConverter : IValueConverter
    {
        private readonly int m_ThumnailScaleWidth = 40;
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (double)value * m_ThumnailScaleWidth;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }

    public class ThumnailSizeHeightConverter : IValueConverter
    {
        private readonly int m_ThumnailScaleHeight = 30;
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (double)value * m_ThumnailScaleHeight;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
