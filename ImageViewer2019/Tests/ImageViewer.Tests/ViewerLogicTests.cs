using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageViewer.Tests
{
    /// <summary>
    /// 並び順(FileSorter)・ガンマ補正(GammaCorrection)・動画の位置指定(VideoPosition)のテスト。
    /// どれも画面に依存しない計算だけ。WinForms版・WPF版のImageViewerで同じ内容のテストを使っている。
    /// </summary>
    [TestClass]
    public class ViewerLogicTests
    {
        // --- FileSorter ---------------------------------------------------------------

        [TestMethod]
        public void 名前順は数字を数として比べる()
        {
            string[] files = { @"C:\x\img10.png", @"C:\x\img2.png", @"C:\x\IMG1.png" };

            CollectionAssert.AreEqual(new[] { @"C:\x\IMG1.png", @"C:\x\img2.png", @"C:\x\img10.png" },
                FileSorter.Sort(files, FileSortOrder.Name, false));
            CollectionAssert.AreEqual(new[] { @"C:\x\img10.png", @"C:\x\img2.png", @"C:\x\IMG1.png" },
                FileSorter.Sort(files, FileSortOrder.Name, true));
        }

        [TestMethod]
        public void サイズ順は小さい順で同じサイズは名前順()
        {
            var sizes = new Dictionary<string, long> { { "c.png", 10 }, { "a.png", 300 }, { "b.png", 10 } };

            string[] result = FileSorter.Sort(sizes.Keys, FileSortOrder.Size, false, file => sizes[file]);

            CollectionAssert.AreEqual(new[] { "b.png", "c.png", "a.png" }, result);
            CollectionAssert.AreEqual(new[] { "a.png", "c.png", "b.png" },
                FileSorter.Sort(sizes.Keys, FileSortOrder.Size, true, file => sizes[file]), "降順は全体を逆にする");
        }

        [TestMethod]
        public void 更新日時順は古い順()
        {
            var times = new Dictionary<string, DateTime>
            {
                { "new.png", new DateTime(2026, 1, 3) },
                { "old.png", new DateTime(2020, 1, 1) },
                { "mid.png", new DateTime(2024, 6, 1) },
            };

            CollectionAssert.AreEqual(new[] { "old.png", "mid.png", "new.png" },
                FileSorter.Sort(times.Keys, FileSortOrder.Date, false, null, file => times[file]));
        }

        [TestMethod]
        public void FromIndexは範囲外を名前順にする()
        {
            Assert.AreEqual(FileSortOrder.Size, FileSorter.FromIndex(1));
            Assert.AreEqual(FileSortOrder.Date, FileSorter.FromIndex(2));
            Assert.AreEqual(FileSortOrder.Name, FileSorter.FromIndex(-1));
            Assert.AreEqual(FileSortOrder.Name, FileSorter.FromIndex(99));
            Assert.AreEqual(3, FileSorter.DisplayNames.Length);
        }

        [TestMethod]
        public void 空やnullを渡しても例外にならない()
        {
            Assert.AreEqual(0, FileSorter.Sort(null, FileSortOrder.Size, false).Length);
            Assert.AreEqual(0, FileSorter.Sort(new string[0], FileSortOrder.Date, true).Length);
        }

        // --- GammaCorrection ----------------------------------------------------------

        [TestMethod]
        public void ガンマ1は変化しない()
        {
            byte[] table = GammaCorrection.BuildTable(1.0);
            for (int i = 0; i < 256; i++)
            {
                Assert.AreEqual(i, table[i]);
            }
            Assert.IsTrue(GammaCorrection.IsIdentity(1.0));
            Assert.IsFalse(GammaCorrection.IsIdentity(1.1));
        }

        [TestMethod]
        public void ガンマを大きくすると中間が明るく小さくすると暗くなり両端は変わらない()
        {
            byte[] bright = GammaCorrection.BuildTable(2.0);
            byte[] dark = GammaCorrection.BuildTable(0.5);

            Assert.AreEqual(128, bright[64], "255×(64/255)^(1/2)≒127.7");
            Assert.AreEqual(16, dark[64], "255×(64/255)^2≒16.1");
            Assert.AreEqual(0, bright[0]);
            Assert.AreEqual(255, bright[255]);
            Assert.AreEqual(0, dark[0]);
            Assert.AreEqual(255, dark[255]);
        }

        [TestMethod]
        public void ApplyToBgraは透明度を変えない()
        {
            byte[] pixels = { 64, 64, 64, 10, 0, 255, 64, 200 };

            GammaCorrection.ApplyToBgra(pixels, GammaCorrection.BuildTable(2.0));

            CollectionAssert.AreEqual(new byte[] { 128, 128, 128, 10, 0, 255, 128, 200 }, pixels);
        }

        [TestMethod]
        public void Clampは範囲外とNaNを丸める()
        {
            Assert.AreEqual(GammaCorrection.MinGamma, GammaCorrection.Clamp(0.0));
            Assert.AreEqual(GammaCorrection.MaxGamma, GammaCorrection.Clamp(10.0));
            Assert.AreEqual(GammaCorrection.DefaultGamma, GammaCorrection.Clamp(double.NaN));
        }

        // --- VideoPosition ------------------------------------------------------------

        [TestMethod]
        public void 時間は秒と分秒と時分秒で書ける()
        {
            TimeSpan time;
            Assert.IsTrue(VideoPosition.TryParseTime("12.5", out time));
            Assert.AreEqual(TimeSpan.FromSeconds(12.5), time);
            Assert.IsTrue(VideoPosition.TryParseTime("1:23.5", out time));
            Assert.AreEqual(TimeSpan.FromSeconds(83.5), time);
            Assert.IsTrue(VideoPosition.TryParseTime("1:02:03", out time));
            Assert.AreEqual(TimeSpan.FromSeconds(3723), time);
            Assert.IsTrue(VideoPosition.TryParseTime(" ", out time));
            Assert.AreEqual(TimeSpan.Zero, time, "空欄は先頭(0秒)");
        }

        [TestMethod]
        public void 時間の書き方がおかしければ読めない()
        {
            TimeSpan time;
            Assert.IsFalse(VideoPosition.TryParseTime("abc", out time));
            Assert.IsFalse(VideoPosition.TryParseTime("-1", out time));
            Assert.IsFalse(VideoPosition.TryParseTime("1:75", out time), "秒の欄は60未満");
            Assert.IsFalse(VideoPosition.TryParseTime("1.5:10", out time), "分は整数");
            Assert.IsFalse(VideoPosition.TryParseTime("1:2:3:4", out time));
        }

        [TestMethod]
        public void コマ番号は1始まりでfpsから時間に直す()
        {
            VideoPosition position;
            string error;

            Assert.IsTrue(VideoPosition.TryParse("31", true, "30", out position, out error));
            Assert.IsTrue(position.IsFrame);
            Assert.AreEqual(31, position.FrameNumber);
            Assert.AreEqual(TimeSpan.FromSeconds(1), position.Time, "31コマ目は30fpsなら1秒の所");

            Assert.IsTrue(VideoPosition.TryParse("1", true, "", out position, out error));
            Assert.AreEqual(TimeSpan.Zero, position.Time, "1コマ目は先頭。fpsが空欄なら30");
            Assert.AreEqual(30.0, position.Fps);
        }

        [TestMethod]
        public void コマ番号やfpsがおかしければエラーの文言を返す()
        {
            VideoPosition position;
            string error;

            Assert.IsFalse(VideoPosition.TryParse("0", true, "30", out position, out error));
            StringAssert.Contains(error, "1以上");
            Assert.IsFalse(VideoPosition.TryParse("1.5", true, "30", out position, out error));
            Assert.IsFalse(VideoPosition.TryParse("10", true, "0", out position, out error));
            StringAssert.Contains(error, "fps");
            Assert.IsFalse(VideoPosition.TryParse("x", false, "30", out position, out error));
            StringAssert.Contains(error, "秒");
        }

        [TestMethod]
        public void 保存するファイル名に付ける位置の文字列()
        {
            Assert.AreEqual("12.500s", VideoPosition.FromTime(TimeSpan.FromSeconds(12.5)).ToFileNameSuffix());
            Assert.AreEqual("f120", VideoPosition.FromFrame(120, 30).ToFileNameSuffix());
        }

        [TestMethod]
        public void ffmpegの引数は時間ならssで探しコマ番号ならselectで0始まりのn番目を取り出す()
        {
            string byTime = VideoPosition.BuildFfmpegArguments(@"C:\v\a b.mp4", VideoPosition.FromTime(TimeSpan.FromSeconds(1.25)), @"C:\t\o.png");
            StringAssert.Contains(byTime, "-ss 1.250 -i \"C:\\v\\a b.mp4\" -frames:v 1 -y \"C:\\t\\o.png\"");

            string byFrame = VideoPosition.BuildFfmpegArguments(@"C:\v\a.mp4", VideoPosition.FromFrame(120, 30), @"C:\t\o.png");
            StringAssert.Contains(byFrame, "-vf \"select=eq(n\\,119)\"");
            Assert.IsFalse(byFrame.Contains("-ss "));
        }
    }
}
