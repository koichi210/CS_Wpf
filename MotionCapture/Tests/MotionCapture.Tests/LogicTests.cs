using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MotionCapture.Tests
{
    /// <summary>
    /// Logic（MFCのDDX_Text/DDV_MinMaxIntと同じ入力チェック、「記録領域の確認」の縮小サイズ）のテスト。
    /// </summary>
    [TestClass]
    public class LogicTests
    {
        private static DialogInput DefaultInput()
        {
            CaptureSettings d = new CaptureSettings();
            return new DialogInput
            {
                SaveFileName = d.SaveFileName,
                CaptRectX = Logic.Format(d.CaptRectX),
                CaptRectY = Logic.Format(d.CaptRectY),
                CaptRectWidth = Logic.Format(d.CaptRectWidth),
                CaptRectHeight = Logic.Format(d.CaptRectHeight),
                CaptBpp = Logic.Format(d.BitmapBpp),
                CaptFps = Logic.Format(d.FrameRate),
                TimeoutSec = Logic.Format(d.TimeoutSec),
                SkipFrame = Logic.Format(d.SkipFrame),
                ResizeRectWidth = Logic.Format(d.ResizeRectWidth),
                ResizeRectHeight = Logic.Format(d.ResizeRectHeight),
                Resize = d.Resize,
                MousePointRec = d.MousePointRec,
            };
        }

        private static void AssertFail(DialogInput input, InputField expectedField, String expectedMessage)
        {
            InputField field;
            String message;

            Boolean ok = Logic.Exchange(input, new CaptureSettings(), out field, out message);

            Assert.IsFalse(ok);
            Assert.AreEqual(expectedField, field);
            Assert.AreEqual(expectedMessage, message);
        }

        // ---- 読み取り（sscanf の %d / %u と同じ） ----

        [TestMethod]
        public void 整数を読む()
        {
            int value;
            Assert.IsNull(Logic.ParseInt("123", out value));
            Assert.AreEqual(123, value);
            Assert.IsNull(Logic.ParseInt("-5", out value));
            Assert.AreEqual(-5, value);
            Assert.IsNull(Logic.ParseInt("+7", out value));
            Assert.AreEqual(7, value);
        }

        [TestMethod]
        public void 前の空白は飛ばし後ろの余計な文字は無視する()
        {
            int value;
            Assert.IsNull(Logic.ParseInt("  \t42", out value));
            Assert.AreEqual(42, value);
            Assert.IsNull(Logic.ParseInt("12abc", out value));
            Assert.AreEqual(12, value);
            Assert.IsNull(Logic.ParseInt("0x10", out value));
            Assert.AreEqual(0, value);
        }

        [TestMethod]
        public void 数字が無ければ整数を入力してくださいになる()
        {
            int value;
            Assert.AreEqual("整数を入力してください。", Logic.ParseInt("", out value));
            Assert.AreEqual("整数を入力してください。", Logic.ParseInt("   ", out value));
            Assert.AreEqual("整数を入力してください。", Logic.ParseInt("abc", out value));
            Assert.AreEqual("整数を入力してください。", Logic.ParseInt("-", out value));
            Assert.AreEqual("整数を入力してください。", Logic.ParseInt("１２", out value), "全角数字は読めない");
        }

        [TestMethod]
        public void 符号なしの欄は読めなければ正の整数を入力してくださいになる()
        {
            uint value;
            Assert.AreEqual("正の整数を入力してください。", Logic.ParseUInt("abc", out value));
            Assert.IsNull(Logic.ParseUInt("10", out value));
            Assert.AreEqual(10u, value);
        }

        [TestMethod]
        public void 符号なしの欄でもマイナスは読めて大きな値になる()
        {
            uint value;
            Assert.IsNull(Logic.ParseUInt("-1", out value));
            Assert.AreEqual(UInt32.MaxValue, value);
        }

        [TestMethod]
        public void 桁あふれは下位32bitになる()
        {
            int value;
            Assert.IsNull(Logic.ParseInt("99999999999", out value));
            Assert.AreEqual(unchecked((int)99999999999L), value);
            uint u;
            Assert.IsNull(Logic.ParseUInt("99999999999999999999999", out u));
            Assert.AreEqual(UInt32.MaxValue, u);
        }

        [TestMethod]
        public void 入力欄は63文字目までしか読まない()
        {
            int value;
            Assert.AreEqual(Logic.ParseIntMessage, Logic.ParseInt(new String(' ', 63) + "5", out value));
            Assert.IsNull(Logic.ParseInt(new String(' ', 62) + "5", out value));
            Assert.AreEqual(5, value);
        }

        // ---- UpdateData(TRUE) ----

        [TestMethod]
        public void 初期値はMFC版と同じでそのまま通る()
        {
            CaptureSettings settings = new CaptureSettings();
            InputField field;
            String message;

            Assert.IsTrue(Logic.Exchange(DefaultInput(), settings, out field, out message));

            Assert.AreEqual(@"c:\ScreenCapture.avi", settings.SaveFileName);
            Assert.AreEqual(0, settings.CaptRectX);
            Assert.AreEqual(0, settings.CaptRectY);
            Assert.AreEqual(1920, settings.CaptRectWidth);
            Assert.AreEqual(1080, settings.CaptRectHeight);
            Assert.AreEqual(24u, settings.BitmapBpp);
            Assert.AreEqual(20, settings.FrameRate);
            Assert.AreEqual(10u, settings.TimeoutSec);
            Assert.AreEqual(0u, settings.SkipFrame);
            Assert.AreEqual(1024, settings.ResizeRectWidth);
            Assert.AreEqual(768, settings.ResizeRectHeight);
            Assert.IsFalse(settings.Resize);
            Assert.IsTrue(settings.MousePointRec);
            Assert.AreEqual(InputField.None, field);
        }

        [TestMethod]
        public void 幅と高さは1から16384まで()
        {
            DialogInput input = DefaultInput();
            input.CaptRectWidth = "0";
            AssertFail(input, InputField.CaptRectWidth, "1 から 16384 までの整数を入力してください。");

            input = DefaultInput();
            input.CaptRectHeight = "16385";
            AssertFail(input, InputField.CaptRectHeight, "1 から 16384 までの整数を入力してください。");

            input = DefaultInput();
            input.ResizeRectWidth = "-1";
            AssertFail(input, InputField.ResizeRectWidth, "1 から 16384 までの整数を入力してください。");

            input = DefaultInput();
            input.ResizeRectHeight = "0";
            AssertFail(input, InputField.ResizeRectHeight, "1 から 16384 までの整数を入力してください。");
        }

        [TestMethod]
        public void bppは0から32まで()
        {
            DialogInput input = DefaultInput();
            input.CaptBpp = "33";
            AssertFail(input, InputField.CaptBpp, "0 から 32 までの整数を入力してください。");

            // UINTの欄なので "-1" は 4294967295 と読まれ、intで比べると-1になって範囲外
            input = DefaultInput();
            input.CaptBpp = "-1";
            AssertFail(input, InputField.CaptBpp, "0 から 32 までの整数を入力してください。");

            input = DefaultInput();
            input.CaptBpp = "0";
            InputField field;
            String message;
            Assert.IsTrue(Logic.Exchange(input, new CaptureSettings(), out field, out message));
        }

        [TestMethod]
        public void FPSは1から120まで()
        {
            DialogInput input = DefaultInput();
            input.CaptFps = "121";
            AssertFail(input, InputField.CaptFps, "1 から 120 までの整数を入力してください。");

            input = DefaultInput();
            input.CaptFps = "0";
            AssertFail(input, InputField.CaptFps, "1 から 120 までの整数を入力してください。");
        }

        [TestMethod]
        public void 秒数とコマ送りは正の整数()
        {
            DialogInput input = DefaultInput();
            input.TimeoutSec = "abc";
            AssertFail(input, InputField.TimeoutSec, "正の整数を入力してください。");

            input = DefaultInput();
            input.SkipFrame = "";
            AssertFail(input, InputField.SkipFrame, "正の整数を入力してください。");
        }

        [TestMethod]
        public void 座標は範囲チェックなしで負数も通る()
        {
            DialogInput input = DefaultInput();
            input.CaptRectX = "-1920";
            input.CaptRectY = "-10";
            CaptureSettings settings = new CaptureSettings();
            InputField field;
            String message;

            Assert.IsTrue(Logic.Exchange(input, settings, out field, out message));
            Assert.AreEqual(-1920, settings.CaptRectX);
            Assert.AreEqual(-10, settings.CaptRectY);
        }

        [TestMethod]
        public void エラーはMFC版のDoDataExchangeの順で最初の1つだけ()
        {
            // X(先) と Width(後) の両方が不正なら X を報告する
            DialogInput input = DefaultInput();
            input.CaptRectX = "x";
            input.CaptRectWidth = "0";
            AssertFail(input, InputField.CaptRectX, "整数を入力してください。");

            // Y は Width より先にチェックする（DDXの並び順）
            input = DefaultInput();
            input.CaptRectY = "y";
            input.CaptRectWidth = "0";
            AssertFail(input, InputField.CaptRectY, "整数を入力してください。");

            // FPS は TimeoutSec より先
            input = DefaultInput();
            input.CaptFps = "999";
            input.TimeoutSec = "abc";
            AssertFail(input, InputField.CaptFps, "1 から 120 までの整数を入力してください。");
        }

        [TestMethod]
        public void エラーより前の欄は書き込み済みになる()
        {
            DialogInput input = DefaultInput();
            input.CaptRectX = "5";
            input.CaptRectWidth = "0";
            CaptureSettings settings = new CaptureSettings();
            InputField field;
            String message;

            Assert.IsFalse(Logic.Exchange(input, settings, out field, out message));

            Assert.AreEqual(5, settings.CaptRectX);
            Assert.AreEqual(0, settings.CaptRectWidth, "DDX_Textで書いてからDDV_MinMaxIntで弾く");
            Assert.AreEqual(1080, settings.CaptRectHeight, "後の欄は元のまま");
        }

        // ---- 記録領域の確認 ----

        [TestMethod]
        public void 確認画像は縦横比を保って700x400に収める()
        {
            int width;
            int height;
            Logic.CalcSampleSize(1920, 1080, out width, out height);
            Assert.AreEqual(700, width);
            Assert.AreEqual(393, height);

            Logic.CalcSampleSize(800, 1600, out width, out height);
            Assert.AreEqual(200, width);
            Assert.AreEqual(400, height);
        }

        [TestMethod]
        public void 小さい領域は拡大せず極端に細くても1ピクセルは残す()
        {
            int width;
            int height;
            Logic.CalcSampleSize(100, 50, out width, out height);
            Assert.AreEqual(100, width);
            Assert.AreEqual(50, height);

            Logic.CalcSampleSize(1, 16384, out width, out height);
            Assert.AreEqual(1, width);
            Assert.AreEqual(400, height);
        }

        [TestMethod]
        public void 確認ダイアログのタイトルに領域を出す()
        {
            Assert.AreEqual("キャプチャ領域 (0, 0) 1920 x 1080", Logic.MakeSampleCaption(0, 0, 1920, 1080));
            Assert.AreEqual("キャプチャ領域 (-10, 20) 30 x 40", Logic.MakeSampleCaption(-10, 20, 30, 40));
        }

        // ---- 保存先の確認 ----

        [TestMethod]
        public void 保存先が他で開かれていたら使えない()
        {
            String dir = Path.Combine(Path.GetTempPath(), "MotionCaptureTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                String path = Path.Combine(dir, "a.avi");
                File.WriteAllBytes(path, new byte[] { 1, 2, 3 });

                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    Assert.IsFalse(MainWindow.CanOpenSaveFile(path));
                }
                Assert.IsFalse(MainWindow.CanOpenSaveFile(Path.Combine(dir, "none", "a.avi")));
                Assert.IsFalse(MainWindow.CanOpenSaveFile(""));
                Assert.IsTrue(MainWindow.CanOpenSaveFile(path), "閉じられた後は使える");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void 保存先の確認では既存のファイルを切り詰めず無ければ何も残さない()
        {
            String dir = Path.Combine(Path.GetTempPath(), "MotionCaptureTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // 以前は確かめるときに空にしていたので、圧縮の選択をキャンセルすると元の中身が消えていた
                String existing = Path.Combine(dir, "existing.avi");
                byte[] content = new byte[] { 1, 2, 3, 4, 5 };
                File.WriteAllBytes(existing, content);
                Assert.IsTrue(MainWindow.CanOpenSaveFile(existing));
                CollectionAssert.AreEqual(content, File.ReadAllBytes(existing));

                // 無いファイルは作って確かめるが、0バイトのファイルは残さない（録画で必要になったら録画側が作る）
                String created = Path.Combine(dir, "new.avi");
                Assert.IsTrue(MainWindow.CanOpenSaveFile(created));
                Assert.IsFalse(File.Exists(created));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
