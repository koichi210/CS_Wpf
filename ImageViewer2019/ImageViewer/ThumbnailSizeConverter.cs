using System;
using System.Collections.Generic;
using System.Windows.Data;

namespace ImageViewer
{
    public class ThumbnailSizeNameConverter : IValueConverter
    {
        private static readonly Dictionary<int, string> _sizeNames = new Dictionary<int, string>()
        { {1, "Small "},
          {2, "Middle"},
          {3, "Large "}
        };

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            int sizeIndex = int.Parse(value.ToString());
            // 範囲外(1〜3以外)は null
            return _sizeNames.TryGetValue(sizeIndex, out string name) ? name : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }

    public class ThumbnailSizeWidthConverter : IValueConverter
    {
        private const int _thumbnailScaleWidth = 40;

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (double)value * _thumbnailScaleWidth;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }

    public class ThumbnailSizeHeightConverter : IValueConverter
    {
        private const int _thumbnailScaleHeight = 30;

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (double)value * _thumbnailScaleHeight;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
