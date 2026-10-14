using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;

namespace ChotChotChat
{
    /// <summary>
    /// 簡易TCPチャットのサンプル(WinForms版ChotChotChatのWPF移植)。
    /// Serverで待ち受け、clientで接続し、送信で1件送るとどちらも切断する(「暫定：毎回コネクト」の仕様はWinForms版のまま)。
    ///
    /// 【スレッド】
    /// WinForms版はソケット処理をUIスレッドで行っていて、Serverを押すとメッセージが届くまで画面が固まっていた。
    /// WPF版は接続待ち・接続・送受信をワーカースレッドで行い、結果(ステータス・ログ・警告)は Dispatcher でUIスレッドへ渡す。
    /// 処理中のボタンは押せないようにして、同じServer/Clientを2つのスレッドが同時に使わないようにしている。
    /// </summary>
    public partial class MainWindow : Window
    {
        private const String _address = "127.0.0.1";

        private readonly Server _server;
        private readonly Client _client;

        // ワーカーで処理中か(UIスレッドだけが読み書きする)
        private bool _serverBusy;
        private bool _clientBusy;

        // 閉じた後に届いた結果は捨てる(ワーカーから見るので volatile)
        private volatile bool _closed;

        public MainWindow()
            : this(Server.DefaultPort)
        {
        }

        // テストでは8080以外(空いているポート)を使うため
        internal MainWindow(int port)
        {
            InitializeComponent();
            _server = new Server(port);
            _client = new Client(port);
            UpdateButtons();
        }

        // テスト用
        internal bool IsServerBusy
        {
            get { return _serverBusy; }
        }

        internal bool IsClientBusy
        {
            get { return _clientBusy; }
        }

        private void button_Server_Click(object sender, RoutedEventArgs e)
        {
            if (_server.IsListening)
            {
                MessageBox.Show(this, "すでにクライアントと接続済みです");
                label_StatusBar.Text = "";
                return;
            }

            // 待ち受けの開始はすぐ終わるのでUIスレッドで行い、ポート使用中などの失敗をその場で知らせる
            try
            {
                label_StatusBar.Text = _server.Listen(_address);
            }
            catch (SocketException ex)
            {
                label_StatusBar.Text = _server.Disconnect();
                ShowWarning("接続を待てませんでした", ex);
                return;
            }

            _serverBusy = true;
            UpdateButtons();

            RunInBackground(() =>
            {
                try
                {
                    String connected = _server.Accept();
                    PostToUi(() => label_StatusBar.Text = connected);

                    String received = _server.Receive();
                    PostToUi(() =>
                    {
                        if (received == null)
                        {
                            MessageBox.Show(this, "Disconnect Client", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        // ログ画面更新
                        AppendLog(received);
                    });
                }
                catch (Exception ex) when (IsNetworkError(ex))
                {
                    PostToUi(() => ShowWarning("メッセージを受信できませんでした", ex));
                }
                finally
                {
                    // 暫定：毎回コネクト(受信に失敗しても接続中のまま残らないよう必ず閉じる)
                    String closed = _server.Disconnect();
                    PostToUi(() =>
                    {
                        label_StatusBar.Text = closed;
                        _serverBusy = false;
                        UpdateButtons();
                    });
                }
            });
        }

        private void button_Client_Click(object sender, RoutedEventArgs e)
        {
            if (_client.IsConnected)
            {
                MessageBox.Show(this, "すでにサーバーと接続済みです");
                label_StatusBar.Text = "";
                return;
            }

            _clientBusy = true;
            UpdateButtons();

            // 相手が居ないと接続の失敗が分かるまで数秒かかるので、ワーカーで接続する
            RunInBackground(() =>
            {
                try
                {
                    String connected = _client.Connect(_address);
                    PostToUi(() => label_StatusBar.Text = connected);
                }
                catch (SocketException ex)
                {
                    PostToUi(() =>
                    {
                        label_StatusBar.Text = "";
                        ShowWarning("サーバーに接続できませんでした", ex);
                    });
                }
                finally
                {
                    PostToUi(() =>
                    {
                        _clientBusy = false;
                        UpdateButtons();
                    });
                }
            });
        }

        private void button_Send_Click(object sender, RoutedEventArgs e)
        {
            if (!_client.IsConnected)
            {
                MessageBox.Show(this, "先にサーバーと接続してください");
                return;
            }

            // テキストボックスはUIスレッドでしか読めないので、ワーカーに渡す前に取り出しておく
            String message = textBox_Message.Text;
            _clientBusy = true;
            UpdateButtons();

            RunInBackground(() =>
            {
                try
                {
                    String reply = _client.Send(message);
                    PostToUi(() =>
                    {
                        if (reply == null)
                        {
                            MessageBox.Show(this, "Disconnect Server", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        AppendLog(message + Environment.NewLine);
                    });
                }
                catch (Exception ex) when (IsNetworkError(ex))
                {
                    PostToUi(() => ShowWarning("メッセージを送信できませんでした", ex));
                }
                finally
                {
                    // 暫定：毎回コネクト(送信に失敗しても接続中のまま残らないよう必ず切断する)
                    String closed = _client.Disconnect();
                    PostToUi(() =>
                    {
                        label_StatusBar.Text = closed;
                        _clientBusy = false;
                        UpdateButtons();
                    });
                }
            });
        }

        // 処理中のものは押せないようにする(Sendは未接続でも押せる。押すと「先にサーバーと接続してください」と案内する)
        private void UpdateButtons()
        {
            button_Server.IsEnabled = !_serverBusy;
            button_Client.IsEnabled = !_clientBusy;
            button_Send.IsEnabled = !_clientBusy;
        }

        private void AppendLog(String text)
        {
            textBox_Log.AppendText(text);
            textBox_Log.ScrollToEnd();
        }

        private void ShowWarning(String message, Exception ex)
        {
            MessageBox.Show(this, message + Environment.NewLine + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // 相手の切断・タイムアウト・閉じる操作による中断など、通信では起こりうる例外
        // (NetworkStream は SocketException を IOException に包んで投げ、閉じた後の操作は ObjectDisposedException になる)
        private static bool IsNetworkError(Exception ex)
        {
            return ex is SocketException || ex is IOException || ex is ObjectDisposedException;
        }

        // ワーカースレッドで実行する。待ち時間が長い(接続待ち等)のでスレッドプールではなく専用スレッドにし、
        // ウィンドウを閉じた時に待ち続けていてもプロセスが終われるよう IsBackground にする。
        // 想定外の例外はUIスレッドで投げ直し、StcWpfExceptionGuard に知らせてもらう(WinForms版で未処理例外が出た時と同じ扱い)
        private void RunInBackground(Action work)
        {
            Thread thread = new Thread(() =>
            {
                try
                {
                    work();
                }
                catch (Exception ex)
                {
                    PostToUi(() => ExceptionDispatchInfo.Capture(ex).Throw());
                }
            });
            thread.IsBackground = true;
            thread.Name = "ChotChotChat.Socket";
            thread.Start();
        }

        // ワーカースレッドからUIスレッドへ処理を渡す。閉じた後なら何もしない
        private void PostToUi(Action action)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_closed)
                {
                    action();
                }
            }));
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // 接続待ち・通信中のワーカーがあれば、ソケットを閉じて抜けさせる(ポート8080も解放される)
            _closed = true;
            _server.Disconnect();
            _client.Disconnect();
        }
    }
}
