using System;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.IO;

namespace ImageViewer
{
    public class ImageConverter : IValueConverter
    {
        private const int _thumbnailDecodePixelWidth = 50;

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // リソースを握ったままになる
            //var img = new BitmapImage();
            //img.BeginInit();
            //img.UriSource = new Uri(value.ToString());
            //img.DecodePixelWidth = _thumbnailDecodePixelWidth;
            //img.EndInit();
            //return img;

            // 解決策1：リソースを解放できる
            //MemoryStream ms = new MemoryStream(File.ReadAllBytes(value.ToString()));
            //WriteableBitmap wb = new WriteableBitmap(BitmapFrame.Create(ms));
            //ms.Close();
            //return wb;

            // 解決策2：リソースを解放できる
            // streamを使うことで画像をメモリ上に展開できるので、リソースを解放することができる
            // (例外時もストリームを閉じるよう using にしている)
            // コンバーター内の例外はバインディングが握りつぶさずアプリごと落ちるため、壊れた画像・読めないファイル
            // (一覧作成後に消された・ロック中など)が1枚でもフォルダにあると落ちていた。読めない画像はサムネイル無し(null)にする
            try
            {
                var img = new BitmapImage();
                using (FileStream stream = File.OpenRead(value.ToString()))
                {
                    img.BeginInit();
                    img.DecodePixelWidth = _thumbnailDecodePixelWidth;
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.StreamSource = stream;
                    img.EndInit();
                }
                return img;
            }
            catch (Exception)
            {
                return null;
            }
        }

        //-----------------------------------------------------------------------------------------------
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
