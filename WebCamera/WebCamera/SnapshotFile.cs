using System;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;

namespace WebCamera
{
    /// <summary>
    /// スナップショット(静止画)保存まわりの、カメラやUIに依存しない小さなロジック。
    /// MainWindow から切り出してあるのは、カメラ無しの環境でもテストできるようにするため。
    /// (ファイル名・フィルタ・初期フォルダはWinForms版と同じ)
    /// </summary>
    public static class SnapshotFile
    {
        public const string Extension = "png";
        public const string DialogFilter = "PNG画像 (*.png)|*.png";

        /// <summary>
        /// 既定の保存ファイル名(例: 20261002_134501.png)を作る。
        /// 和暦などのカルチャに左右されないよう、日付書式はインバリアントカルチャで固定する。
        /// </summary>
        public static string CreateDefaultFileName(DateTime now)
        {
            return now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "." + Extension;
        }

        /// <summary>
        /// 保存ダイアログの初期フォルダ。マイピクチャが取れない環境ではマイドキュメントにする。
        /// </summary>
        public static string GetInitialDirectory()
        {
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (!string.IsNullOrEmpty(pictures))
            {
                return pictures;
            }
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        /// <summary>
        /// 画像をPNGで保存する(既存ファイルは上書き。上書き確認は保存ダイアログ側で済んでいる前提)。
        /// </summary>
        public static void SavePng(BitmapSource image, string path)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(stream);
            }
        }
    }
}
