using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StandardTemplate.Wpf.Tests;

namespace MotionCapture.Tests
{
    /// <summary>
    /// MainWindow / SampleCaptAreaWindow を実際に生成して、MFC版のダイアログと同じ構成になっているかを確認するテスト。
    /// 表示はせず、作って中身を見るだけにする。
    /// 確認と録画の結果の出し方のテストだけは、一時フォルダへ非圧縮で小さく録画し、メッセージボックスを差し替えて確かめる。
    /// </summary>
    [TestClass]
    public class MainWindowTests
    {
        private const String _closeConfirm = "実行中です。中断して閉じますか？";
        private const String _stopConfirm = "実行中です。中断しますか？";
        private const String _timeoutMessage = "タイムアウトが発生しました。\n記録を停止しファイルを保存しました。";

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

        [TestMethod]
        public void 起動直後はMFC版と同じ初期値が入っている()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();

                Assert.AreEqual("MotionCapture", window.Title);
                Assert.AreEqual(@"c:\ScreenCapture.avi", Text(window, "textBox_SaveFileName"));
                Assert.AreEqual("0", Text(window, "textBox_CaptRectX"));
                Assert.AreEqual("0", Text(window, "textBox_CaptRectY"));
                Assert.AreEqual("1920", Text(window, "textBox_CaptRectWidth"));
                Assert.AreEqual("1080", Text(window, "textBox_CaptRectHeight"));
                Assert.AreEqual("24", Text(window, "textBox_CaptBpp"));
                Assert.AreEqual("20", Text(window, "textBox_CaptFps"));
                Assert.AreEqual("0", Text(window, "textBox_SkipFrame"));
                Assert.AreEqual("10", Text(window, "textBox_TimeoutSec"));
                Assert.AreEqual("1024", Text(window, "textBox_ResizeRectWidth"));
                Assert.AreEqual("768", Text(window, "textBox_ResizeRectHeight"));
                Assert.AreEqual(false, ((CheckBox)window.FindName("checkBox_Resize")).IsChecked);
                Assert.AreEqual(true, ((CheckBox)window.FindName("checkBox_MousePointRec")).IsChecked);
            });
        }

        [TestMethod]
        public void ボタンとチェックボックスの文言がMFC版と同じ()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();

                Assert.AreEqual("記録領域の確認", ((Button)window.FindName("button_CaptAreaSample")).Content);
                Assert.AreEqual("記録開始", ((Button)window.FindName("button_Record")).Content);
                Assert.AreEqual("記録停止", ((Button)window.FindName("button_RecordStop")).Content);
                Assert.AreEqual("リサイズ有効", ((CheckBox)window.FindName("checkBox_Resize")).Content);
                Assert.AreEqual("マウスポインタを記録", ((CheckBox)window.FindName("checkBox_MousePointRec")).Content);
                Assert.AreEqual(ResizeMode.CanMinimize, window.ResizeMode);
            });
        }

        [TestMethod]
        public void 記録領域の確認ダイアログは領域をタイトルに出し縮小画像を表示する()
        {
            StaRunner.Run(() =>
            {
                SampleCaptAreaWindow window = new SampleCaptAreaWindow(0, 0, 1920, 1080);

                Assert.AreEqual("キャプチャ領域 (0, 0) 1920 x 1080", window.Title);
                BitmapSource image = (BitmapSource)((Image)window.FindName("image_Sample")).Source;
                Assert.AreEqual(700, image.PixelWidth);
                Assert.AreEqual(393, image.PixelHeight);
                Button close = (Button)window.FindName("button_Close");
                Assert.AreEqual("閉じる", close.Content);
                Assert.IsTrue(close.IsDefault);
                Assert.IsTrue(close.IsCancel);
            });
        }

        // ---- 確認を出している間に届いた録画の結果 ----

        [TestMethod]
        public void 閉じる確認の間に録画が終わったら確認に重ねず_いいえの後に結果を出す()
        {
            String path = Path.Combine(_workDir, "close_no.avi");
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();
                List<String> shown = new List<String>();
                ManageAvi avi = null;
                window.ShowMessage = (text, caption, button) =>
                {
                    shown.Add(text);
                    if (button == MessageBoxButton.YesNo)
                    {
                        // 確認を出している間に録画が最後まで進み、結果が届く
                        WaitForRecordAndDoEvents(avi);
                        Assert.AreEqual(1, shown.Count, "確認の上に結果を重ねて出さない");
                        return MessageBoxResult.No;
                    }
                    return MessageBoxResult.OK;
                };
                avi = StartRecording(window, path, 1);

                Assert.IsFalse(window.StopRecordForClose(), "いいえなら閉じない");

                CollectionAssert.AreEqual(new[] { _closeConfirm, _timeoutMessage }, shown);
                DoEvents();
                Assert.AreEqual(2, shown.Count, "預かった結果は1回だけ出す");
            });
            Assert.AreEqual(10, AviReader.Read(path).Length);
        }

        [TestMethod]
        public void 閉じる確認の間に録画が終わっても_はいなら結果を出さずに閉じられる()
        {
            String path = Path.Combine(_workDir, "close_yes.avi");
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();
                List<String> shown = new List<String>();
                ManageAvi avi = null;
                window.ShowMessage = (text, caption, button) =>
                {
                    shown.Add(text);
                    if (button == MessageBoxButton.YesNo)
                    {
                        WaitForRecordAndDoEvents(avi);
                        return MessageBoxResult.Yes;
                    }
                    return MessageBoxResult.OK;
                };
                avi = StartRecording(window, path, 1);

                // 録画はもう終わっているので、待ち続けずにすぐ戻る
                Assert.IsTrue(window.StopRecordForClose(), "はいなら閉じる");

                DoEvents();
                CollectionAssert.AreEqual(new[] { _closeConfirm }, shown, "閉じると答えたら結果は出さない");
                Assert.IsFalse(avi.IsExecution());
            });
            Assert.AreEqual(10, AviReader.Read(path).Length);
        }

        [TestMethod]
        public void 記録開始の確認の間に録画が終わったら確認に重ねず_答えた後に結果を出す()
        {
            foreach (MessageBoxResult answer in new[] { MessageBoxResult.No, MessageBoxResult.Yes })
            {
                String path = Path.Combine(_workDir, "start_" + answer + ".avi");
                StaRunner.Run(() =>
                {
                    MainWindow window = new MainWindow();
                    List<String> shown = new List<String>();
                    ManageAvi avi = null;
                    window.ShowMessage = (text, caption, button) =>
                    {
                        shown.Add(text);
                        if (button == MessageBoxButton.YesNo)
                        {
                            WaitForRecordAndDoEvents(avi);
                            Assert.AreEqual(1, shown.Count, "確認の上に結果を重ねて出さない");
                            return answer;
                        }
                        return MessageBoxResult.OK;
                    };
                    avi = StartRecording(window, path, 1);

                    // 録画中に「記録開始」を押すと「実行中です。中断しますか？」を出す
                    ((Button)window.FindName("button_Record")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                    CollectionAssert.AreEqual(new[] { _stopConfirm, _timeoutMessage }, shown, "答え: " + answer);
                });
            }
        }

        [TestMethod]
        public void 閉じる確認の間に録画が終わらなければ_いいえの後は何も出さず結果はいつもどおり出す()
        {
            String path = Path.Combine(_workDir, "close_no_running.avi");
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();
                List<String> shown = new List<String>();
                window.ShowMessage = (text, caption, button) =>
                {
                    shown.Add(text);
                    return button == MessageBoxButton.YesNo ? MessageBoxResult.No : MessageBoxResult.OK;
                };
                ManageAvi avi = StartRecording(window, path, 100);

                Assert.IsFalse(window.StopRecordForClose());
                CollectionAssert.AreEqual(new[] { _closeConfirm }, shown);
                Assert.IsTrue(avi.IsRecording(), "いいえなら録画を続ける");

                avi.StopRecord();
                WaitForRecordAndDoEvents(avi);
                CollectionAssert.AreEqual(new[] { _closeConfirm, "ファイルを保存しました。" }, shown);
            });
        }

        // 一時フォルダへ非圧縮で録画を始める（8×8、10FPS、コマ送り10＝1フレーム100ms）
        private static ManageAvi StartRecording(MainWindow window, String path, uint sec)
        {
            ManageAvi avi = window.Avi;
            avi.SetSaveFileName(path);
            avi.SetCaptureRect(0, 0, 8, 8);
            avi.SetFrameRate(10);
            avi.SetRecordSec(sec);
            avi.SetSkipFrame(10);
            avi.SetCaptureQuality(24);
            avi.SetCompress(false);
            avi.SetResize(false, 0, 0);
            avi.SetRecordMousePoint(false);
            avi.StartRecord();
            return avi;
        }

        // 録画スレッドが終わるまで待ち、UIスレッドへ回された結果の通知を処理させる
        private static void WaitForRecordAndDoEvents(ManageAvi avi)
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
            DoEvents();
        }

        private static void DoEvents()
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
        }

        private static String Text(Window window, String name)
        {
            return ((TextBox)window.FindName(name)).Text;
        }
    }
}
