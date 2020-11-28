using System;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.IO;

namespace ImageViewerForWpf
{
    public class ImageConverter : IValueConverter
    {
        private readonly int m_ThumailDecodePixelWidth = 50;
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // リソースを握ったままになる
            //var img = new BitmapImage();
            //img.BeginInit();
            //img.UriSource = new Uri(value.ToString());
            //img.DecodePixelWidth = m_ThumailDecodePixelWidth;
            //img.EndInit();
            //return img;

            // 解決策1：リソースを解放できる
            //MemoryStream ms = new MemoryStream(File.ReadAllBytes(value.ToString()));
            //WriteableBitmap wb = new WriteableBitmap(BitmapFrame.Create(ms));
            //ms.Close();
            //return wb;

            // 解決策2：リソースを解放できる
            // streamを使うことで画像をメモリ上に展開できるので、リソースを解放することができる
            var img = new BitmapImage();
            FileStream stream = File.OpenRead(value.ToString());
            img.BeginInit();
            img.DecodePixelWidth = m_ThumailDecodePixelWidth;
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = stream;
            img.EndInit();
            stream.Close();
            return img;
        }

        //-----------------------------------------------------------------------------------------------
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
