using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StandardTemplate.Wpf.Tests;

namespace WebCamera.Tests
{
    /// <summary>
    /// MainWindow を実際に生成して、WinForms版と同じ画面構成・ボタン状態・終了処理になっているかを確認するテスト。
    ///
    /// 本物のカメラ(VideoCapture(0))は、カメラがある環境ではテスト実行機の物理カメラを掴んでしまい、
    /// 無い環境では完了時に MessageBox を出して止まるので使わない。
    /// 代わりに FrameSourceFactory へ FakeFrameSource を差し込み、停止要求で正常に止まる経路だけを動かす。
    /// このアプリは設定を保存しないので、Close() しても本物のユーザー設定には触れない。
    /// </summary>
    [TestClass]
    public class MainWindowTests
    {
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

        private static T Find<T>(Window window, string name)
        {
            return (T)window.FindName(name);
        }

        private static void Click(Button button)
        {
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }

        // Dispatcher に積まれた処理(ワーカーからのフレーム・完了通知)を、条件を満たすまで回す
        private static void PumpUntil(Func<bool> condition, string message)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (!condition())
            {
                if (watch.Elapsed > _timeout)
                {
                    Assert.Fail(message);
                }
                DispatcherFrame frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                    new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                Thread.Sleep(5);
            }
        }

        // 画面外に、アクティブにせず表示する(Closing/Closed を本物の流れで発生させるため)
        private static void ShowOffscreen(Window window)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000;
            window.Top = -20000;
            window.ShowActivated = false;
            window.ShowInTaskbar = false;
            window.Show();
        }

        private static CameraFrame CreateFrame()
        {
            return new CameraFrame(2, 1, 6, PixelFormats.Bgr24, new byte[] { 1, 2, 3, 4, 5, 6 });
        }

        private static CaptureWorker CreateIdleWorker()
        {
            return new CaptureWorker(() => new FakeFrameSource(), (w, f) => { }, (w, r) => { });
        }

        [TestMethod]
        public void 初期状態はStart表示でSnapshotは無効()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();

                Assert.AreEqual("WebCamera", window.Title);
                Button startStop = Find<Button>(window, "button_StartStop");
                Button snapshot = Find<Button>(window, "button_Snapshot");
                Assert.AreEqual("Start", startStop.Content);
                Assert.IsTrue(startStop.IsEnabled);
                Assert.AreEqual("Snapshot", snapshot.Content);
                Assert.IsFalse(snapshot.IsEnabled, "映像が無いうちは保存できない");
            });
        }

        [TestMethod]
        public void プレビューは黒背景で縦横比を保って表示する()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();

                Image preview = Find<Image>(window, "image_Preview");
                Assert.AreEqual(Stretch.Uniform, preview.Stretch);
                Border border = (Border)preview.Parent;
                Assert.AreEqual(Colors.Black, ((SolidColorBrush)border.Background).Color);
            });
        }

        [TestMethod]
        public void TabIndexは左から順()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();

                Assert.AreEqual(1, Find<Button>(window, "button_StartStop").TabIndex);
                Assert.AreEqual(2, Find<Button>(window, "button_Snapshot").TabIndex);
            });
        }

        [TestMethod]
        public void ボタンにWinForms版と同じツールチップが付いている()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();

                Assert.AreEqual("PCの1台目のカメラの映像表示を開始/停止する", Find<Button>(window, "button_StartStop").ToolTip);
                Assert.AreEqual("押した瞬間に表示中の映像をPNGで保存する。停止後も最後に表示した映像を保存できる",
                    Find<Button>(window, "button_Snapshot").ToolTip);
            });
        }

        [TestMethod]
        public void フレームを受け取るとプレビューに表示されSnapshotが有効になる()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();
                CaptureWorker worker = CreateIdleWorker();

                window.OnFrameReady(worker, CreateFrame());

                BitmapSource shown = (BitmapSource)Find<Image>(window, "image_Preview").Source;
                Assert.AreEqual(2, shown.PixelWidth);
                Assert.IsTrue(Find<Button>(window, "button_Snapshot").IsEnabled);
                Assert.AreEqual(-1, worker.PendingFrames, "表示したことをワーカーへ知らせている");
            });
        }

        [TestMethod]
        public void 同じサイズの次のフレームは同じBitmapへ描き直す()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();
                CaptureWorker worker = CreateIdleWorker();
                Image preview = Find<Image>(window, "image_Preview");

                window.OnFrameReady(worker, CreateFrame());
                ImageSource first = preview.Source;
                window.OnFrameReady(worker, CreateFrame());

                Assert.AreSame(first, preview.Source);
            });
        }

        [TestMethod]
        public void ワーカー未起動ならClosingは閉じるのを止めない()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();
                CancelEventArgs args = new CancelEventArgs(false);

                MethodInfo handler = typeof(MainWindow).GetMethod("Window_Closing", BindingFlags.Instance | BindingFlags.NonPublic);
                handler.Invoke(window, new object[] { window, args });

                Assert.IsFalse(args.Cancel);
            });
        }

        [TestMethod]
        public void Startで映像が表示されStopでカメラを解放して止まる()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();
                FakeFrameSource source = null;
                window.FrameSourceFactory = () => source = new FakeFrameSource();
                Button startStop = Find<Button>(window, "button_StartStop");
                Image preview = Find<Image>(window, "image_Preview");
                ShowOffscreen(window);
                try
                {
                    Click(startStop);
                    Assert.IsTrue(window.IsCapturing);
                    Assert.AreEqual("Stop", startStop.Content);

                    PumpUntil(() => preview.Source != null, "映像が表示されない");
                    Assert.IsTrue(Find<Button>(window, "button_Snapshot").IsEnabled);

                    Click(startStop);
                    Assert.IsFalse(startStop.IsEnabled, "停止処理中は押せない");

                    PumpUntil(() => !window.IsCapturing, "ワーカーが止まらない");
                    Assert.AreEqual("Start", startStop.Content);
                    Assert.IsTrue(startStop.IsEnabled);
                    Assert.IsTrue(source.IsDisposed, "カメラはワーカーが解放している");
                    Assert.IsTrue(Find<Button>(window, "button_Snapshot").IsEnabled, "停止後も最後の映像は保存できる");
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [TestMethod]
        public void 映像表示中に閉じるとワーカーの終了を待ってから閉じる()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();
                FakeFrameSource source = null;
                window.FrameSourceFactory = () => source = new FakeFrameSource();
                bool closed = false;
                window.Closed += (s, e) => closed = true;
                ShowOffscreen(window);

                Click(Find<Button>(window, "button_StartStop"));
                PumpUntil(() => Find<Image>(window, "image_Preview").Source != null, "映像が表示されない");

                window.Close();

                Assert.IsFalse(closed, "ワーカーが止まるまでは閉じない");
                Assert.IsTrue(window.IsVisible);
                Assert.IsFalse(Find<Button>(window, "button_StartStop").IsEnabled);
                Assert.IsFalse(Find<Button>(window, "button_Snapshot").IsEnabled);

                PumpUntil(() => closed, "ワーカーが止まった後に閉じ直していない");
                Assert.IsTrue(source.IsDisposed, "閉じる前にカメラはワーカーが解放している");
                Assert.IsNull(Find<Image>(window, "image_Preview").Source, "閉じたら映像を手放す");
            });
        }
    }
}
