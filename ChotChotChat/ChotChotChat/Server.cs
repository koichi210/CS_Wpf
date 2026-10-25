using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ChotChotChat
{
    /// <summary>
    /// 1件だけメッセージを受け取るTCPサーバー。
    ///
    /// WinForms版は Connect(待ち受け+接続受け入れ)と Receive をUIスレッドで呼んでいて、待っている間は画面が固まっていた。
    /// WPF版はワーカースレッドから呼べるよう、UI(MessageBox・ログ欄)には触らず、結果を戻り値で返す形にした。
    /// 待ち受けを止める Disconnect は別スレッド(UI)から呼ばれるので、フィールドはロックで守る。
    /// </summary>
    internal sealed class Server
    {
        internal const int DefaultPort = 8080;
        private const int _timeoutMsec = 100000;

        private readonly int _port;
        private readonly object _lock = new object();
        private TcpListener _listener;
        private TcpClient _client;

        public Server()
            : this(DefaultPort)
        {
        }

        // テストでは8080以外(空いているポート)を使うため
        internal Server(int port)
        {
            _port = port;
        }

        // 待ち受け中、または接続中(=Disconnect するまで次の待ち受けはできない)
        public bool IsListening
        {
            get
            {
                lock (_lock)
                {
                    return _listener != null;
                }
            }
        }

        /// <summary>
        /// 待ち受けを開始する(すぐ戻る)。ポートが使用中などで失敗したら SocketException。
        /// </summary>
        public String Listen(String listenAddress)
        {
            // ListenするIPアドレス
            IPAddress ipAddress = IPAddress.Parse(listenAddress);

            lock (_lock)
            {
                if (_listener != null)
                {
                    throw new InvalidOperationException("すでにクライアントと接続済みです");
                }

                TcpListener listener = new TcpListener(ipAddress, _port);
                // Listenを開始
                listener.Start();
                _listener = listener;

                return String.Format("{0}:{1}で接続を待っています…", ipAddress, ((IPEndPoint)listener.LocalEndpoint).Port);
            }
        }

        /// <summary>
        /// 接続要求が来るまで待って受け入れる(ワーカースレッドで呼ぶ)。
        /// 待っている間に Disconnect されると SocketException か ObjectDisposedException になる。
        /// </summary>
        public String Accept()
        {
            TcpListener listener;
            lock (_lock)
            {
                listener = _listener;
            }
            if (listener == null)
            {
                throw new InvalidOperationException("待ち受けを開始していません");
            }

            // 接続要求があったら受け入れる
            TcpClient client = listener.AcceptTcpClient();

            lock (_lock)
            {
                if (_listener != listener)
                {
                    // 受け入れた直後に Disconnect された
                    client.Close();
                    throw new ObjectDisposedException(nameof(Server));
                }
                _client = client;
            }

            IPEndPoint remote = (IPEndPoint)client.Client.RemoteEndPoint;
            return String.Format("クライアント({0}:{1})と接続しました。", remote.Address, remote.Port);
        }

        /// <summary>
        /// クライアントからメッセージを1件受信し、受信した文字数を応答する(ワーカースレッドで呼ぶ)。
        /// 受信した文字列(改行込み)を返す。応答前にクライアントが切断していたら null。
        /// </summary>
        public String Receive()
        {
            TcpClient client;
            lock (_lock)
            {
                client = _client;
            }
            if (client == null)
            {
                throw new InvalidOperationException("クライアントと接続していません");
            }

            Encoding enc = Encoding.UTF8;

            // NetworkStreamを取得
            using (NetworkStream ns = client.GetStream())
            {
                // タイムアウト
                ns.ReadTimeout = _timeoutMsec;
                ns.WriteTimeout = _timeoutMsec;

                // クライアントから送られたデータ受信
                byte[] received = NetworkStreamReader.ReceiveMessage(ns);
                if (received == null)
                {
                    return null;
                }

                // 受信したデータを文字列に変換
                String resMsg = enc.GetString(received);

                // クライアントにデータ送信(受信した文字数を返す)
                byte[] sendBytes = enc.GetBytes(resMsg.Length.ToString() + '\n');
                ns.Write(sendBytes, 0, sendBytes.Length);

                return resMsg;
            }
        }

        public String Disconnect()
        {
            // 接続待ちの途中で失敗した場合もポートを解放するため、接続状態に関係なく閉じる。
            // 別スレッドが AcceptTcpClient で待っていても、Stop すればそちらは例外で抜ける
            lock (_lock)
            {
                if (_client != null)
                {
                    _client.Close();
                    _client = null;
                }
                if (_listener != null)
                {
                    _listener.Stop();
                    _listener = null;
                }
            }
            return "クライアントとの接続を閉じました。";
        }
    }
}
