using System;
using System.Collections.Generic;
using System.Windows.Data;

namespace ImageViewer
{
    public class ThumbnailSizeNameConverter : IValueConverter
    {
        // Dictionaryを使用せずswitchを使用するときに使用
        // private readonly string m_ThumbnailSizeStrS = "Small";
        // private readonly string m_ThumbnailSizeStrM = "Middle";
        // private readonly string m_ThumbnailSizeStrL = "Large";

        private readonly Dictionary<int, string> m_SizeNames = new Dictionary<int, string>()
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

            Int32 sizeIndex = Int32.Parse(value.ToString());
            // TODO：XamlのSliderThumbnail値から範囲値を取得したい
            if (sizeIndex < 1 || 3 < sizeIndex )
            {
                return null;
            }

            return m_SizeNames[sizeIndex];
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }

    public class ThumbnailSizeWidthConverter : IValueConverter
    {
        private readonly int m_ThumbnailScaleWidth = 40;
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (double)value * m_ThumbnailScaleWidth;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }

    public class ThumbnailSizeHeightConverter : IValueConverter
    {
        private readonly int m_ThumbnailScaleHeight = 30;
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (double)value * m_ThumbnailScaleHeight;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
