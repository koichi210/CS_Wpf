using System;
using System.Net;
using System.Net.Sockets;

namespace ChotChotChat.Tests
{
    /// <summary>
    /// テストで使う、このPC内(127.0.0.1)だけで完結するTCP接続のヘルパー。
    /// 8080番は本物のアプリや他の作業で使われていることがあるので、テストでは毎回空いているポートを使う。
    /// </summary>
    internal sealed class LoopbackTcp : IDisposable
    {
        private LoopbackTcp(TcpClient clientSide, TcpClient serverSide)
        {
            ClientSide = clientSide;
            ServerSide = serverSide;
        }

        public TcpClient ClientSide { get; }

        public TcpClient ServerSide { get; }

        // 接続済みのソケットの組を作る
        public static LoopbackTcp Connect()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                TcpClient client = new TcpClient();
                client.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
                TcpClient server = listener.AcceptTcpClient();
                return new LoopbackTcp(client, server);
            }
            finally
            {
                listener.Stop();
            }
        }

        // いま空いているポート番号(一度借りてすぐ返す)
        public static int FindFreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        // そのポートで待ち受けできる(=誰も使っていない)か
        public static bool CanListen(int port)
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            try
            {
                listener.Start();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            finally
            {
                listener.Stop();
            }
        }

        public void Dispose()
        {
            ClientSide.Close();
            ServerSide.Close();
        }
    }
}
