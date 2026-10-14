using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ChotChotChat
{
    /// <summary>
    /// サーバーへ1件だけメッセージを送るTCPクライアント。
    ///
    /// WinForms版はUIスレッドで呼ばれ、中で MessageBox も出していた。WPF版はワーカースレッドから呼べるよう、
    /// UIには触らず結果を戻り値・例外で返す。閉じる時の Disconnect は別スレッド(UI)から呼ばれるので、ロックで守る。
    /// </summary>
    internal sealed class Client
    {
        private const int _timeoutMsec = 100000;

        private readonly int _port;
        private readonly object _lock = new object();
        private TcpClient _tcp;

        public Client()
            : this(Server.DefaultPort)
        {
        }

        // テストでは8080以外(空いているポート)を使うため
        internal Client(int port)
        {
            _port = port;
        }

        public bool IsConnected
        {
            get
            {
                lock (_lock)
                {
                    return _tcp != null;
                }
            }
        }

        /// <summary>
        /// サーバーと接続する。サーバーが待っていなければ SocketException。
        /// </summary>
        public String Connect(String hostName)
        {
            if (IsConnected)
            {
                throw new InvalidOperationException("すでにサーバーと接続済みです");
            }

            // TcpClientを作成し、サーバーと接続する
            TcpClient tcp = new TcpClient(hostName, _port);
            lock (_lock)
            {
                _tcp = tcp;
            }

            IPEndPoint remote = (IPEndPoint)tcp.Client.RemoteEndPoint;
            IPEndPoint local = (IPEndPoint)tcp.Client.LocalEndPoint;
            return String.Format("サーバー({0}:{1})と接続しました({2}:{3})。",
                remote.Address, remote.Port, local.Address, local.Port);
        }

        public String Disconnect()
        {
            lock (_lock)
            {
                if (_tcp != null)
                {
                    _tcp.Close();
                    _tcp = null;
                }
            }
            return "サーバーと切断しました。";
        }

        /// <summary>
        /// メッセージを送信し、サーバーの応答(受信した文字数)を受け取って返す。
        /// 応答の前にサーバーが切断したら null。通信エラーは IOException 等の例外になる。
        /// </summary>
        public String Send(String sendText)
        {
            TcpClient tcp;
            lock (_lock)
            {
                tcp = _tcp;
            }
            if (tcp == null)
            {
                throw new InvalidOperationException("先にサーバーと接続してください");
            }

            // NetworkStreamを取得
            using (NetworkStream ns = tcp.GetStream())
            {
                // タイムアウト
                ns.ReadTimeout = _timeoutMsec;
                ns.WriteTimeout = _timeoutMsec;

                // データ送信
                byte[] sendBytes = Encoding.UTF8.GetBytes(sendText + Environment.NewLine);
                ns.Write(sendBytes, 0, sendBytes.Length);

                // サーバーから送られたデータ(応答)を受信する
                byte[] reply = NetworkStreamReader.ReceiveLine(ns);
                return reply == null ? null : Encoding.UTF8.GetString(reply);
            }
        }
    }
}
