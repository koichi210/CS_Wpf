using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageViewer
{
    // 表示対象のファイルの判定と、画像の読み込み・ガンマ補正(WPF向け)
    public static class ImageFiles
    {
        // 拡張子の大文字小文字は区別しない
        private static readonly HashSet<string> _imageExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".bmp", ".png", ".tiff", ".tif", ".gif" };

        // 動画として扱う拡張子(WinForms版ImageViewerのPreview.csと同じ。TypeScriptと被る.tsは入れていない)
        private static readonly HashSet<string> _videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".m4v", ".mov", ".avi", ".wmv", ".asf", ".mkv", ".webm",
            ".mpg", ".mpeg", ".m2ts", ".mts", ".flv", ".3gp", ".3g2",
        };

        // 一覧のサムネイルを作るときの横幅(サムネイルの最大表示幅Large=120に合わせる)
        private const int _thumbnailDecodePixelWidth = 120;

        public static bool IsImageFile(string filePath)
        {
            return !string.IsNullOrEmpty(filePath) && _imageExtensions.Contains(Path.GetExtension(filePath));
        }

        public static bool IsVideoFile(string filePath)
        {
            return !string.IsNullOrEmpty(filePath) && _videoExtensions.Contains(Path.GetExtension(filePath));
        }

        // フォルダ直下の画像と動画を、指定の並び順で返す
        public static string[] GetTargetFiles(string folderPath, FileSortOrder order, bool descending)
        {
            IEnumerable<string> files = Directory.GetFiles(folderPath)
                .Where(file => IsImageFile(file) || IsVideoFile(file));
            return FileSorter.Sort(files, order, descending);
        }

        // 画像ファイルを読む(ファイルを握ったままにしないようメモリへ読み込む)。読めなければnull。
        // 別スレッドからUIスレッドへ渡せるようFreezeする。decodePixelWidthが0なら元の大きさ
        public static BitmapSource LoadImage(string filePath, int decodePixelWidth)
        {
            try
            {
                BitmapImage image = new BitmapImage();
                using (FileStream stream = File.OpenRead(filePath))
                {
                    image.BeginInit();
                    if (decodePixelWidth > 0)
                    {
                        image.DecodePixelWidth = decodePixelWidth;
                    }
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                }
                image.Freeze();
                return image;
            }
            catch (Exception)
            {
                // 壊れた画像・読めないファイル(一覧作成後に消された・ロック中など)はサムネイル無しにする
                return null;
            }
        }

        // 一覧のサムネイル。画像は縮小して読み、動画はシェルのサムネイル(先頭付近のコマ)
        public static ImageSource LoadThumbnail(string filePath)
        {
            if (IsVideoFile(filePath))
            {
                return ShellThumbnail.GetThumbnail(filePath, _thumbnailDecodePixelWidth, _thumbnailDecodePixelWidth);
            }
            return LoadImage(filePath, _thumbnailDecodePixelWidth);
        }

        // ガンマ補正した画像を返す(1.0ならそのまま返す)。補正の計算はGammaCorrection(テスト済み)
        public static BitmapSource ApplyGamma(BitmapSource source, double gamma)
        {
            if (source == null || GammaCorrection.IsIdentity(gamma))
            {
                return source;
            }

            FormatConvertedBitmap bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int stride = bgra.PixelWidth * 4;
            byte[] pixels = new byte[stride * bgra.PixelHeight];
            bgra.CopyPixels(pixels, stride, 0);
            GammaCorrection.ApplyToBgra(pixels, GammaCorrection.BuildTable(gamma));

            BitmapSource result = BitmapSource.Create(bgra.PixelWidth, bgra.PixelHeight, source.DpiX, source.DpiY,
                PixelFormats.Bgra32, null, pixels, stride);
            result.Freeze();
            return result;
        }

        // PNGで保存する
        public static void SavePng(BitmapSource source, string filePath)
        {
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (FileStream stream = File.Create(filePath))
            {
                encoder.Save(stream);
            }
        }
    }
}
