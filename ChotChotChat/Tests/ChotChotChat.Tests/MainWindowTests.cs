using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StandardTemplate.Wpf.Tests;

namespace ChotChotChat.Tests
{
    /// <summary>
    /// MainWindow を実際に生成して、WinForms版と同じ画面構成になっているか、
    /// ボタン操作でワーカー→Dispatcher→画面の流れが動くかを確認するテスト。
    ///
    /// ポート8080ではなく空いているポートを使う(内部コンストラクタで指定)。
    /// MessageBox が出る経路(未接続での送信・接続失敗・切断検知)は、テストが止まるので動かさない。
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

        // Dispatcher に積まれた処理(ワーカーからの結果)を、条件を満たすまで回す
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

        // 画面外に、アクティブにせず表示する
        private static void ShowOffscreen(Window window)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000;
            window.Top = -20000;
            window.ShowActivated = false;
            window.ShowInTaskbar = false;
            window.Show();
        }

        [TestMethod]
        public void タイトルとボタンがWinForms版と同じ()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();

                Assert.AreEqual("ちょっと！ちょっとチャット！", window.Title);
                Assert.AreEqual("Server", Find<Button>(window, "button_Server").Content);
                Assert.AreEqual("client", Find<Button>(window, "button_Client").Content);
                Assert.AreEqual("送信", Find<Button>(window, "button_Send").Content);
                Assert.IsTrue(Find<TextBox>(window, "textBox_Log").IsReadOnly);
                Assert.AreEqual("", Find<TextBlock>(window, "label_StatusBar").Text);
            });
        }

        [TestMethod]
        public void ボタンにツールチップが付いている()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();

                StringAssert.StartsWith((string)Find<Button>(window, "button_Server").ToolTip,
                    "このPC(127.0.0.1)のポート8080で接続を待ち、メッセージを1件受信したら切断する。");
                Assert.AreEqual("このPC(127.0.0.1)のポート8080で待っているサーバーに接続する",
                    Find<Button>(window, "button_Client").ToolTip);
                Assert.AreEqual("メッセージを送信し、サーバーの応答を受け取ったら切断する。次に送る前にclientで接続し直す必要がある",
                    Find<Button>(window, "button_Send").ToolTip);
            });
        }

        [TestMethod]
        public void TabIndexは左上から右上_左下_右下の順()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();

                Assert.AreEqual(0, Find<Button>(window, "button_Server").TabIndex);
                Assert.AreEqual(1, Find<Button>(window, "button_Client").TabIndex);
                Assert.AreEqual(2, Find<TextBox>(window, "textBox_Log").TabIndex);
                Assert.AreEqual(3, Find<TextBox>(window, "textBox_Message").TabIndex);
                Assert.AreEqual(4, Find<Button>(window, "button_Send").TabIndex);
            });
        }

        [TestMethod]
        public void 同じ画面でServerとclientをつないで送信するとログに残り両方切断される()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow(LoopbackTcp.FindFreePort());
                Button server = Find<Button>(window, "button_Server");
                Button client = Find<Button>(window, "button_Client");
                Button send = Find<Button>(window, "button_Send");
                TextBox log = Find<TextBox>(window, "textBox_Log");
                TextBlock status = Find<TextBlock>(window, "label_StatusBar");
                ShowOffscreen(window);
                try
                {
                    Click(server);
                    Assert.IsTrue(window.IsServerBusy);
                    Assert.IsFalse(server.IsEnabled, "接続を待っている間はもう一度押せない");
                    StringAssert.EndsWith(status.Text, "で接続を待っています…");

                    // WinForms版と違い、接続を待っている間も画面は動いている
                    Click(client);
                    Assert.IsFalse(client.IsEnabled);
                    PumpUntil(() => !window.IsClientBusy, "クライアントの接続が終わらない");
                    Assert.IsTrue(client.IsEnabled);

                    Find<TextBox>(window, "textBox_Message").Text = "テストです";
                    Click(send);
                    PumpUntil(() => !window.IsClientBusy && !window.IsServerBusy, "送受信が終わらない");

                    // サーバー側の受信ログとクライアント側の送信ログが、同じ画面に1行ずつ残る
                    Assert.AreEqual("テストです\r\nテストです\r\n", log.Text);
                    Assert.IsTrue(status.Text == "サーバーと切断しました。" || status.Text == "クライアントとの接続を閉じました。",
                        status.Text);
                    Assert.IsTrue(server.IsEnabled);
                    Assert.IsTrue(send.IsEnabled);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [TestMethod]
        public void 接続待ちのまま閉じるとポートを解放する()
        {
            StaRunner.Run(() =>
            {
                int port = LoopbackTcp.FindFreePort();
                MainWindow window = new MainWindow(port);
                ShowOffscreen(window);

                Click(Find<Button>(window, "button_Server"));
                Assert.IsFalse(LoopbackTcp.CanListen(port), "前提: 待ち受け中");

                window.Close();

                Assert.IsTrue(LoopbackTcp.CanListen(port), "閉じたのにポートが使われたまま");
                // 待っていたワーカーの後始末(閉じた後なので画面には何も出さない)を流しておく
                Stopwatch watch = Stopwatch.StartNew();
                PumpUntil(() => watch.ElapsedMilliseconds > 200, "");
            });
        }
    }
}
