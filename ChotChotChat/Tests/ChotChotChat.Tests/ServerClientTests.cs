using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChotChotChat.Tests
{
    /// <summary>
    /// Server/Client をループバック(127.0.0.1)で実際につないで送受信するテスト。
    /// 8080番ではなく、テストごとに空いているポートを使う。
    /// </summary>
    [TestClass]
    public class ServerClientTests
    {
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

        private int _port;
        private Server _server;
        private Client _client;

        [TestInitialize]
        public void Setup()
        {
            _port = LoopbackTcp.FindFreePort();
            _server = new Server(_port);
            _client = new Client(_port);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _client.Disconnect();
            _server.Disconnect();
        }

        private static T Wait<T>(Task<T> task)
        {
            Assert.IsTrue(task.Wait(_timeout), "時間内に終わらない");
            return task.Result;
        }

        [TestMethod]
        public void 待ち受けて接続するとそれぞれ接続先を表示する()
        {
            string listening = _server.Listen("127.0.0.1");
            Assert.AreEqual("127.0.0.1:" + _port + "で接続を待っています…", listening);
            Assert.IsTrue(_server.IsListening);

            Task<string> accept = Task.Run(() => _server.Accept());
            string clientStatus = _client.Connect("127.0.0.1");
            string serverStatus = Wait(accept);

            Assert.IsTrue(_client.IsConnected);
            StringAssert.StartsWith(clientStatus, "サーバー(127.0.0.1:" + _port + ")と接続しました(127.0.0.1:");
            StringAssert.StartsWith(serverStatus, "クライアント(127.0.0.1:");
            StringAssert.EndsWith(serverStatus, ")と接続しました。");
        }

        [TestMethod]
        public void 送ったメッセージをサーバーが受け取り受信文字数を応答する()
        {
            _server.Listen("127.0.0.1");
            Task<string> accept = Task.Run(() => _server.Accept());
            _client.Connect("127.0.0.1");
            Wait(accept);

            Task<string> receive = Task.Run(() => _server.Receive());
            string reply = _client.Send("こんにちは");
            string received = Wait(receive);

            // クライアントは改行(CRLF)を付けて送り、サーバーは改行込みの文字数を返す(WinForms版と同じ)
            Assert.AreEqual("こんにちは\r\n", received);
            Assert.AreEqual("7\n", reply);
        }

        [TestMethod]
        public void 送る前にクライアントが切断したらサーバーはnullを返す()
        {
            _server.Listen("127.0.0.1");
            Task<string> accept = Task.Run(() => _server.Accept());
            _client.Connect("127.0.0.1");
            Wait(accept);

            Assert.AreEqual("サーバーと切断しました。", _client.Disconnect());
            Assert.IsFalse(_client.IsConnected);

            Assert.IsNull(_server.Receive());
        }

        [TestMethod]
        public void 応答前にサーバーが切断したらクライアントはnullか通信エラー()
        {
            _server.Listen("127.0.0.1");
            Task<string> accept = Task.Run(() => _server.Accept());
            _client.Connect("127.0.0.1");
            Wait(accept);

            Assert.AreEqual("クライアントとの接続を閉じました。", _server.Disconnect());

            // 相手が閉じた後の送信は、OSのタイミング次第で「切断(0バイト受信)」か「リセット(IOException)」になる
            try
            {
                Assert.IsNull(_client.Send("届かない"));
            }
            catch (IOException)
            {
            }
        }

        [TestMethod]
        public void 接続待ちの途中でDisconnectするとAcceptは例外で抜けポートも解放される()
        {
            _server.Listen("127.0.0.1");
            Task<string> accept = Task.Run(() => _server.Accept());
            Thread.Sleep(100);

            _server.Disconnect();

            AggregateException error = Assert.ThrowsException<AggregateException>(() => accept.Wait(_timeout));
            Assert.IsTrue(error.InnerException is SocketException || error.InnerException is ObjectDisposedException,
                error.InnerException.ToString());
            Assert.IsFalse(_server.IsListening);
            Assert.IsTrue(LoopbackTcp.CanListen(_port), "ポートが解放されていない");
        }

        [TestMethod]
        public void 待ち受け中にもう一度待ち受けはできない()
        {
            _server.Listen("127.0.0.1");

            InvalidOperationException error = Assert.ThrowsException<InvalidOperationException>(() => _server.Listen("127.0.0.1"));
            Assert.AreEqual("すでにクライアントと接続済みです", error.Message);
        }

        [TestMethod]
        public void 切断後はもう一度待ち受けできる()
        {
            _server.Listen("127.0.0.1");
            _server.Disconnect();

            _server.Listen("127.0.0.1");
            Assert.IsTrue(_server.IsListening);
        }

        [TestMethod]
        public void 接続済みのクライアントはもう一度接続できない()
        {
            _server.Listen("127.0.0.1");
            Task<string> accept = Task.Run(() => _server.Accept());
            _client.Connect("127.0.0.1");
            Wait(accept);

            InvalidOperationException error = Assert.ThrowsException<InvalidOperationException>(() => _client.Connect("127.0.0.1"));
            Assert.AreEqual("すでにサーバーと接続済みです", error.Message);
        }

        [TestMethod]
        public void サーバーが待っていなければ接続はSocketException()
        {
            Assert.ThrowsException<SocketException>(() => _client.Connect("127.0.0.1"));
            Assert.IsFalse(_client.IsConnected);
        }

        [TestMethod]
        public void 未接続での送信と受信は例外()
        {
            Assert.ThrowsException<InvalidOperationException>(() => _client.Send("x"));
            Assert.ThrowsException<InvalidOperationException>(() => _server.Receive());
            Assert.ThrowsException<InvalidOperationException>(() => _server.Accept());
        }

        [TestMethod]
        public void 未接続でも切断はできてメッセージを返す()
        {
            Assert.AreEqual("サーバーと切断しました。", _client.Disconnect());
            Assert.AreEqual("クライアントとの接続を閉じました。", _server.Disconnect());
        }

        [TestMethod]
        public void 既定のポートはWinForms版と同じ8080()
        {
            Assert.AreEqual(8080, Server.DefaultPort);
        }
    }
}
