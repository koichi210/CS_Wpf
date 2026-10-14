using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WebCamera.Tests
{
    /// <summary>
    /// スナップショット保存まわりの、カメラやUIに依存しないロジックのテスト。
    /// ファイル名・フィルタ・初期フォルダのテストはWinForms版WebCamera.Testsと同じ内容。
    /// </summary>
    [TestClass]
    public class SnapshotFileTests
    {
        [TestMethod]
        public void 既定ファイル名は日時入りのpng()
        {
            string name = SnapshotFile.CreateDefaultFileName(new DateTime(2026, 10, 2, 13, 45, 1));
            Assert.AreEqual("20261002_134501.png", name);
        }

        [TestMethod]
        public void 既定ファイル名は一桁の月日時分秒をゼロ埋めし24時間表記()
        {
            Assert.AreEqual("20260102_030405.png", SnapshotFile.CreateDefaultFileName(new DateTime(2026, 1, 2, 3, 4, 5)));
            Assert.AreEqual("20261231_235959.png", SnapshotFile.CreateDefaultFileName(new DateTime(2026, 12, 31, 23, 59, 59)));
        }

        [TestMethod]
        public void 既定ファイル名は和暦カルチャでも西暦になる()
        {
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                CultureInfo japanese = new CultureInfo("ja-JP");
                japanese.DateTimeFormat.Calendar = new JapaneseCalendar();
                Thread.CurrentThread.CurrentCulture = japanese;

                Assert.AreEqual("20261002_134501.png", SnapshotFile.CreateDefaultFileName(new DateTime(2026, 10, 2, 13, 45, 1)));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [TestMethod]
        public void 保存ダイアログの設定はpng()
        {
            Assert.AreEqual("png", SnapshotFile.Extension);
            StringAssert.EndsWith(SnapshotFile.DialogFilter, "|*.png");
        }

        [TestMethod]
        public void 初期フォルダはマイピクチャ()
        {
            string expected = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrEmpty(expected))
            {
                expected = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            Assert.AreEqual(expected, SnapshotFile.GetInitialDirectory());
        }

        [TestMethod]
        public void カメラ映像と同じBgr24の画像をPNGで保存して読み戻せる()
        {
            string folder = Path.Combine(Path.GetTempPath(), "WebCamera.Tests." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string path = Path.Combine(folder, SnapshotFile.CreateDefaultFileName(new DateTime(2026, 10, 15, 9, 0, 0)));
                byte[] pixels = { 10, 20, 30, 40, 50, 60 };
                BitmapSource image = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgr24, null, pixels, 6);

                SnapshotFile.SavePng(image, path);

                using (FileStream stream = File.OpenRead(path))
                {
                    BitmapFrame loaded = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
                    Assert.AreEqual(2, loaded.PixelWidth);
                    Assert.AreEqual(1, loaded.PixelHeight);

                    FormatConvertedBitmap bgr = new FormatConvertedBitmap(loaded, PixelFormats.Bgr24, null, 0);
                    byte[] actual = new byte[6];
                    bgr.CopyPixels(actual, 6, 0);
                    CollectionAssert.AreEqual(pixels, actual);
                }
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        public void 既存ファイルは上書きする()
        {
            string path = Path.Combine(Path.GetTempPath(), "WebCamera.Tests." + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                File.WriteAllText(path, "これは画像ではない長いダミーの中身。上書きで消えるはず。これは画像ではない長いダミーの中身。");
                BitmapSource image = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Gray8, null, new byte[] { 128 }, 1);

                SnapshotFile.SavePng(image, path);

                byte[] head = new byte[8];
                using (FileStream stream = File.OpenRead(path))
                {
                    stream.Read(head, 0, head.Length);
                }
                // PNGのシグネチャ
                CollectionAssert.AreEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, head);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
