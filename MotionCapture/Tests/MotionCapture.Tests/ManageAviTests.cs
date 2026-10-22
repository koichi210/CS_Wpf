using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MotionCapture.Tests
{
    /// <summary>
    /// ManageAvi（VfWでの録画）のテスト。小さい領域・数フレームを一時フォルダへ非圧縮で録画し、
    /// ファイルができること・VfWで読み戻したヘッダー(フレームレート・フレーム数・大きさ)が設定どおりかを確かめる。
    /// 圧縮(ICCompressorChoose)はダイアログが出るので、テストではSetCompress(false)にする
    /// （止めたときに選択ダイアログを閉じられるかのテストだけは圧縮ありで動かし、キャンセルさせる）。
    /// </summary>
    [TestClass]
    public class ManageAviTests
    {
        private String _workDir;

        [TestInitialize]
        public void Setup()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "MotionCaptureTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_workDir))
            {
                Directory.Delete(_workDir, true);
            }
        }

        private ManageAvi MakeAvi(String path, int width, int height, uint fps, uint sec)
        {
            ManageAvi avi = new ManageAvi();
            avi.SetSaveFileName(path);
            avi.SetCaptureRect(0, 0, width, height);
            avi.SetFrameRate(fps);
            avi.SetRecordSec(sec);
            avi.SetSkipFrame(0);
            avi.SetCaptureQuality(24);
            avi.SetCompress(false);
            avi.SetResize(false, 0, 0);
            avi.SetRecordMousePoint(false);
            return avi;
        }

        // VfWはCOMを使うので、アプリと同じくSTAスレッドで動かす
        private static void RunSta(Action action)
        {
            Exception error = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error != null)
            {
                throw new AssertFailedException(error.ToString());
            }
        }

        [TestMethod]
        public void 小さい領域を数フレーム録画するとAVIファイルができる()
        {
            String path = Path.Combine(_workDir, "small.avi");
            ManageAvi avi = MakeAvi(path, 32, 24, 5, 1);
            AviError err = AviError.FileOpen;
            Boolean completed = false;

            RunSta(() => err = avi.RecordSync(out completed));

            Assert.AreEqual(AviError.Success, err);
            Assert.IsTrue(completed, "停止していないので最後のフレームまで録画している");
            Assert.IsTrue(File.Exists(path));
            byte[] head = new byte[12];
            using (FileStream stream = File.OpenRead(path))
            {
                stream.Read(head, 0, head.Length);
            }
            Assert.AreEqual("RIFF", Encoding.ASCII.GetString(head, 0, 4));
            Assert.AreEqual("AVI ", Encoding.ASCII.GetString(head, 8, 4));
            // 非圧縮24bitなので、5フレーム分の画素(32×24×3バイト)より大きい
            Assert.IsTrue(new FileInfo(path).Length > 32 * 24 * 3 * 5);
        }

        [TestMethod]
        public void ヘッダーのフレームレートとフレーム数は設定どおり()
        {
            String path = Path.Combine(_workDir, "header.avi");
            ManageAvi avi = MakeAvi(path, 16, 8, 3, 2);
            AviInfo info = null;

            RunSta(() =>
            {
                Boolean completed;
                Assert.AreEqual(AviError.Success, avi.RecordSync(out completed));
                info = AviReader.Read(path);
            });

            Assert.AreEqual(3u, info.Rate);
            Assert.AreEqual(1u, info.Scale);
            Assert.AreEqual(6, info.Length, "録画秒数×FPSのフレーム数");
            Assert.AreEqual(NativeMethods.comptypeDIB, info.FccHandler);
            Assert.AreEqual(16, info.Width);
            Assert.AreEqual(8, info.Height);
            Assert.AreEqual(24, info.BitCount);
        }

        [TestMethod]
        public void リサイズするとリサイズ後の大きさで書かれマウスポインタも描ける()
        {
            String path = Path.Combine(_workDir, "resize.avi");
            ManageAvi avi = MakeAvi(path, 40, 30, 2, 1);
            avi.SetResize(true, 20, 15);
            avi.SetRecordMousePoint(true);
            AviInfo info = null;

            RunSta(() =>
            {
                Boolean completed;
                Assert.AreEqual(AviError.Success, avi.RecordSync(out completed));
                info = AviReader.Read(path);
            });

            Assert.AreEqual(20, info.Width);
            Assert.AreEqual(15, info.Height);
            Assert.AreEqual(2, info.Length);
        }

        [TestMethod]
        public void 保存先のフォルダが無ければファイルオープンエラー()
        {
            String path = Path.Combine(_workDir, "none", "x.avi");
            ManageAvi avi = MakeAvi(path, 8, 8, 1, 1);
            AviError err = AviError.Success;

            RunSta(() =>
            {
                Boolean completed;
                err = avi.RecordSync(out completed);
            });

            Assert.AreEqual(AviError.FileOpen, err);
            Assert.AreEqual("AVIファイルが開けませんでした。" + path, ManageAvi.MakeErrorMessage(err, path));
            Assert.IsFalse(File.Exists(path));
        }

        [TestMethod]
        public void 録画用の画像が作れないbppなら書かずにエラーにしファイルを残さない()
        {
            // 以前は空の画像バッファのまま書き続け、「保存しました」と出していた
            String path = Path.Combine(_workDir, "bpp2.avi");
            ManageAvi avi = MakeAvi(path, 8, 8, 2, 1);
            avi.SetCaptureQuality(2);
            AviError err = AviError.Success;

            RunSta(() =>
            {
                Boolean completed;
                err = avi.RecordSync(out completed);
            });

            Assert.AreEqual(AviError.CreateBitmap, err);
            Assert.IsFalse(File.Exists(path), "1フレームも書けなかったファイルは消す");
        }

        [TestMethod]
        public void 最後まで録画するとタイムアウトのメッセージを出す()
        {
            String path = Path.Combine(_workDir, "timeout.avi");
            ManageAvi avi = MakeAvi(path, 8, 8, 2, 1);
            String text = null;
            String caption = null;
            avi.Notify = (t, c) => { text = t; caption = c; };

            avi.StartRecord();
            Assert.IsTrue(avi.IsExecution());
            WaitForStop(avi);

            Assert.AreEqual("タイムアウトが発生しました。\n記録を停止しファイルを保存しました。", text);
            Assert.AreEqual("Warning", caption);
            Assert.AreEqual(2, AviReader.Read(path).Length);
        }

        [TestMethod]
        public void 途中で停止すると保存しましたのメッセージを出す()
        {
            String path = Path.Combine(_workDir, "stop.avi");
            // 100秒×10FPS、コマ送り10(=100ms待ち)なので止めなければ100秒以上かかる
            ManageAvi avi = MakeAvi(path, 8, 8, 10, 100);
            avi.SetSkipFrame(10);
            String text = null;
            String caption = null;
            avi.Notify = (t, c) => { text = t; caption = c; };

            avi.StartRecord();
            Thread.Sleep(300);
            avi.StopRecord();
            WaitForStop(avi);

            Assert.AreEqual("ファイルを保存しました。", text);
            Assert.AreEqual("Information", caption);
            int length = AviReader.Read(path).Length;
            Assert.IsTrue(length > 0 && length < 1000, "フレーム数: " + length);
        }

        [TestMethod]
        public void 録画中にStopRecordAndWaitするとファイルを閉じ終わってから戻る()
        {
            String path = Path.Combine(_workDir, "wait.avi");
            ManageAvi avi = MakeAvi(path, 8, 8, 10, 100);
            avi.SetSkipFrame(10);
            String text = null;
            avi.Notify = (t, c) => text = t;

            avi.StartRecord();
            Thread.Sleep(300);
            Assert.IsTrue(avi.IsRecording());
            RunWithTimeout(avi.StopRecordAndWait);

            // 戻った時点で録画スレッドは終わっていて、ファイルはそのまま読める（閉じられていないと壊れたAVIになる）
            Assert.IsFalse(avi.IsExecution());
            Assert.AreEqual("ファイルを保存しました。", text);
            int length = AviReader.Read(path).Length;
            Assert.IsTrue(length > 0 && length < 1000, "フレーム数: " + length);
        }

        [TestMethod]
        public void 停止直後の後片付け中でもStopRecordAndWaitはファイルを閉じ終わるまで待つ()
        {
            String path = Path.Combine(_workDir, "cleanup.avi");
            ManageAvi avi = MakeAvi(path, 8, 8, 10, 100);
            avi.SetSkipFrame(10);
            avi.Notify = (t, c) => { };

            avi.StartRecord();
            Thread.Sleep(300);
            avi.StopRecord();
            Assert.IsFalse(avi.IsRecording(), "停止後は録画中ではない（閉じるときに確認を出さない）");
            RunWithTimeout(avi.StopRecordAndWait);

            Assert.IsFalse(avi.IsExecution());
            Assert.IsTrue(AviReader.Read(path).Length > 0);
        }

        [TestMethod]
        public void 録画していなければStopRecordAndWaitはすぐ戻る()
        {
            ManageAvi avi = new ManageAvi();

            RunWithTimeout(avi.StopRecordAndWait);

            Assert.IsFalse(avi.IsRecording());
            Assert.IsFalse(avi.IsExecution());
        }

        [TestMethod]
        public void 圧縮の選択ダイアログを開いている間に止めるとキャンセルして終わる()
        {
            String path = Path.Combine(_workDir, "choose.avi");
            ManageAvi avi = MakeAvi(path, 8, 8, 10, 100);
            avi.SetCompress(true);
            String text = null;
            String caption = null;
            avi.Notify = (t, c) => { text = t; caption = c; };

            avi.StartRecord();
            Stopwatch watch = Stopwatch.StartNew();
            while (!avi.IsChoosingCompressor && watch.Elapsed < TimeSpan.FromSeconds(10))
            {
                Thread.Sleep(20);
            }
            Assert.IsTrue(avi.IsChoosingCompressor, "圧縮の選択ダイアログを出すところまで進む");
            Thread.Sleep(500);  // ダイアログが開ききるのを待つ
            Assert.IsFalse(File.Exists(path), "保存先のファイルは圧縮方式が決まってから作る");
            RunWithTimeout(avi.StopRecordAndWait);

            Assert.IsFalse(avi.IsExecution());
            Assert.AreEqual("圧縮設定がキャンセルされました。", text);
            Assert.AreEqual("Error", caption);
            Assert.IsFalse(File.Exists(path), "キャンセルなら保存先に空のファイルを残さない");
        }

        [TestMethod]
        public void 圧縮の選択をキャンセルしても既存のファイルの中身は変わらない()
        {
            String path = Path.Combine(_workDir, "existing.avi");
            byte[] content = Encoding.ASCII.GetBytes("ORIGINAL-CONTENT");
            File.WriteAllBytes(path, content);
            ManageAvi avi = MakeAvi(path, 8, 8, 10, 100);
            avi.SetCompress(true);
            String text = null;
            avi.Notify = (t, c) => text = t;

            avi.StartRecord();
            Stopwatch watch = Stopwatch.StartNew();
            while (!avi.IsChoosingCompressor && watch.Elapsed < TimeSpan.FromSeconds(10))
            {
                Thread.Sleep(20);
            }
            Thread.Sleep(500);  // ダイアログが開ききるのを待つ
            RunWithTimeout(avi.StopRecordAndWait);

            Assert.AreEqual("圧縮設定がキャンセルされました。", text);
            CollectionAssert.AreEqual(content, File.ReadAllBytes(path), "以前は0バイトに切り詰められていた");
        }

        [TestMethod]
        public void 開始してすぐ止めても1フレームは書いて読めるAVIにする()
        {
            String path = Path.Combine(_workDir, "quickstop.avi");
            ManageAvi avi = MakeAvi(path, 8, 8, 10, 100);
            avi.SetSkipFrame(10);
            String text = null;
            avi.Notify = (t, c) => text = t;

            avi.StartRecord();
            avi.StopRecord();
            WaitForStop(avi);

            Assert.AreEqual("ファイルを保存しました。", text);
            Assert.IsTrue(AviReader.Read(path).Length >= 1, "フレームのないAVIを残さない");
        }

        [TestMethod]
        public void 録画秒数が0でも1フレームは書く()
        {
            String path = Path.Combine(_workDir, "zero.avi");
            ManageAvi avi = MakeAvi(path, 8, 8, 10, 0);
            AviError err = AviError.FileOpen;
            Boolean completed = false;

            RunSta(() => err = avi.RecordSync(out completed));

            Assert.AreEqual(AviError.Success, err);
            Assert.IsTrue(completed);
            Assert.AreEqual(1, AviReader.Read(path).Length);
        }

        [TestMethod]
        public void 開始してすぐ止めても圧縮の選択ダイアログで止まったままにならない()
        {
            // 選択ダイアログが開く前後のどこで止めても、待っている間に伝え直すので必ず終わる
            for (int i = 0; i < 3; i++)
            {
                String path = Path.Combine(_workDir, "quick" + i + ".avi");
                ManageAvi avi = MakeAvi(path, 8, 8, 10, 100);
                avi.SetCompress(true);
                String text = null;
                avi.Notify = (t, c) => text = t;

                avi.StartRecord();
                Thread.Sleep(i * 50);
                RunWithTimeout(avi.StopRecordAndWait);

                Assert.IsFalse(avi.IsExecution());
                Assert.AreEqual("圧縮設定がキャンセルされました。", text);
                Assert.IsFalse(File.Exists(path), "キャンセルなら保存先に空のファイルを残さない");
            }
        }

        [TestMethod]
        public void コマ送りはフレーム間の待ち時間に直す()
        {
            ManageAvi avi = new ManageAvi();

            avi.SetSkipFrame(0);
            Assert.AreEqual(1, avi.SkipFrameMs);
            avi.SetSkipFrame(10);
            Assert.AreEqual(100, avi.SkipFrameMs);
            avi.SetSkipFrame(3);
            Assert.AreEqual(333, avi.SkipFrameMs);
            avi.SetSkipFrame(2000);
            Assert.AreEqual(0, avi.SkipFrameMs);
        }

        [TestMethod]
        public void 画像サイズは1行を4バイト境界にそろえて計算する()
        {
            Assert.AreEqual(32u * 3 * 24, ManageAvi.CalcSizeImage(32, 24, 24));
            Assert.AreEqual(100u * 10, ManageAvi.CalcSizeImage(33, 10, 24));
            Assert.AreEqual(0u, ManageAvi.CalcSizeImage(33, 10, 0));
            Assert.AreEqual(1920u * 1080 * 4, ManageAvi.CalcSizeImage(1920, 1080, 32));
        }

        [TestMethod]
        public void マウスポインタの位置はリサイズの倍率とホットスポットで決める()
        {
            Assert.AreEqual(90, ManageAvi.CalcCursorPos(110, 10, 1.0f, 10));
            Assert.AreEqual(50, ManageAvi.CalcCursorPos(110, 10, 0.5f, 0));
            Assert.AreEqual(-5, ManageAvi.CalcCursorPos(5, 10, 1.0f, 0));
        }

        [TestMethod]
        public void エラーメッセージはMFC版と同じ()
        {
            Assert.AreEqual("Streamが生成できませんでした。", ManageAvi.MakeErrorMessage(AviError.CreateStream, "a"));
            Assert.AreEqual("圧縮設定がキャンセルされました。", ManageAvi.MakeErrorMessage(AviError.CancelCompress, "a"));
            Assert.AreEqual("圧縮Streamが生成できませんでした。", ManageAvi.MakeErrorMessage(AviError.CreateCompressStream, "a"));
            Assert.AreEqual("録画用の画像が作成できませんでした。bpp の設定を確認してください。", ManageAvi.MakeErrorMessage(AviError.CreateBitmap, "a"));
            Assert.AreEqual("AVIファイルに書き込めませんでした。ディスクの空き容量を確認してください。\na", ManageAvi.MakeErrorMessage(AviError.WriteFrame, "a"));
        }

        // 待ち続けて止まらないときはテストを失敗にする（録画スレッドはバックグラウンドなので、テストの終了で消える）
        private static void RunWithTimeout(Action action)
        {
            Exception error = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            thread.IsBackground = true;
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(30)))
            {
                Assert.Fail("録画スレッドの終了を待ちきれない");
            }
            if (error != null)
            {
                throw new AssertFailedException(error.ToString());
            }
        }

        private static void WaitForStop(ManageAvi avi)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (avi.IsExecution())
            {
                if (watch.Elapsed > TimeSpan.FromSeconds(30))
                {
                    Assert.Fail("録画が終わらない");
                }
                Thread.Sleep(20);
            }
        }
    }
}
