using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChotChotChat.Tests
{
    /// <summary>
    /// NetworkStreamReader.ReceiveLine(改行まで受信する共通ループ)のテスト。ループバックの本物のソケットで送受信する。
    /// </summary>
    [TestClass]
    public class NetworkStreamReaderTests
    {
        private static void Write(NetworkStream ns, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            ns.Write(bytes, 0, bytes.Length);
        }

        [TestMethod]
        public void 改行までの1行を受信する()
        {
            using (LoopbackTcp tcp = LoopbackTcp.Connect())
            {
                NetworkStream sender = tcp.ClientSide.GetStream();
                NetworkStream receiver = tcp.ServerSide.GetStream();
                receiver.ReadTimeout = 5000;

                Write(sender, "hello\r\n");

                byte[] received = NetworkStreamReader.ReceiveLine(receiver);
                Assert.AreEqual("hello\r\n", Encoding.UTF8.GetString(received));
            }
        }

        [TestMethod]
        public void 改行が来るまで分割された受信をつなげる()
        {
            using (LoopbackTcp tcp = LoopbackTcp.Connect())
            {
                NetworkStream sender = tcp.ClientSide.GetStream();
                NetworkStream receiver = tcp.ServerSide.GetStream();
                receiver.ReadTimeout = 5000;

                Thread writer = new Thread(() =>
                {
                    Write(sender, "abc");
                    Thread.Sleep(200);
                    Write(sender, "def\n");
                });
                writer.Start();

                byte[] received = NetworkStreamReader.ReceiveLine(receiver);
                writer.Join();
                Assert.AreEqual("abcdef\n", Encoding.UTF8.GetString(received));
            }
        }

        [TestMethod]
        public void 受信バッファより長い行も全部受け取る()
        {
            using (LoopbackTcp tcp = LoopbackTcp.Connect())
            {
                NetworkStream sender = tcp.ClientSide.GetStream();
                NetworkStream receiver = tcp.ServerSide.GetStream();
                receiver.ReadTimeout = 5000;
                string longText = new string(Enumerable.Range(0, 3000).Select(i => (char)('a' + i % 26)).ToArray()) + "\n";

                Thread writer = new Thread(() => Write(sender, longText));
                writer.Start();

                byte[] received = NetworkStreamReader.ReceiveLine(receiver);
                writer.Join();
                Assert.AreEqual(longText, Encoding.UTF8.GetString(received));
            }
        }

        [TestMethod]
        public void 日本語のUTF8も壊さずに受け取る()
        {
            using (LoopbackTcp tcp = LoopbackTcp.Connect())
            {
                NetworkStream sender = tcp.ClientSide.GetStream();
                NetworkStream receiver = tcp.ServerSide.GetStream();
                receiver.ReadTimeout = 5000;

                Write(sender, "ちょっと！ちょっとチャット！\r\n");

                byte[] received = NetworkStreamReader.ReceiveLine(receiver);
                Assert.AreEqual("ちょっと！ちょっとチャット！\r\n", Encoding.UTF8.GetString(received));
            }
        }

        [TestMethod]
        public void 改行の前に相手が切断したらnull()
        {
            using (LoopbackTcp tcp = LoopbackTcp.Connect())
            {
                NetworkStream receiver = tcp.ServerSide.GetStream();
                receiver.ReadTimeout = 5000;

                Write(tcp.ClientSide.GetStream(), "途中まで");
                tcp.ClientSide.Client.Shutdown(SocketShutdown.Send);

                Assert.IsNull(NetworkStreamReader.ReceiveLine(receiver));
            }
        }

        [TestMethod]
        public void 何も送らずに切断されたらnull()
        {
            using (LoopbackTcp tcp = LoopbackTcp.Connect())
            {
                NetworkStream receiver = tcp.ServerSide.GetStream();
                receiver.ReadTimeout = 5000;

                tcp.ClientSide.Close();

                Assert.IsNull(NetworkStreamReader.ReceiveLine(receiver));
            }
        }

        // ---- メッセージ(バイト数付き)の送受信 ----

        [TestMethod]
        public void 改行を含むメッセージが途中の改行で分かれて届いても1件で受け取る()
        {
            using (LoopbackTcp tcp = LoopbackTcp.Connect())
            {
                NetworkStream sender = tcp.ClientSide.GetStream();
                NetworkStream receiver = tcp.ServerSide.GetStream();
                receiver.ReadTimeout = 5000;
                byte[] body = Encoding.UTF8.GetBytes("1行目\r\n2行目\r\n");
                byte[] header = Encoding.ASCII.GetBytes(body.Length + "\n");

                // 以前は「最後が\nで、まだ届いていないデータが無い」で終わりとみなしていたので、1行目だけで切れていた
                Thread writer = new Thread(() =>
                {
                    sender.Write(header, 0, header.Length);
                    sender.Write(body, 0, 9);   // "1行目\r\n" まで
                    Thread.Sleep(300);
                    sender.Write(body, 9, body.Length - 9);
                });
                writer.Start();

                byte[] received = NetworkStreamReader.ReceiveMessage(receiver);
                writer.Join();
                Assert.AreEqual("1行目\r\n2行目\r\n", Encoding.UTF8.GetString(received));
            }
        }

        [TestMethod]
        public void WriteMessageで送ったメッセージをReceiveMessageで受け取る()
        {
            using (LoopbackTcp tcp = LoopbackTcp.Connect())
            {
                NetworkStream sender = tcp.ClientSide.GetStream();
                NetworkStream receiver = tcp.ServerSide.GetStream();
                receiver.ReadTimeout = 5000;
                string longText = new string(Enumerable.Range(0, 3000).Select(i => i % 50 == 0 ? '\n' : (char)('a' + i % 26)).ToArray());

                Thread writer = new Thread(() =>
                {
                    NetworkStreamReader.WriteMessage(sender, Encoding.UTF8.GetBytes(longText));
                    NetworkStreamReader.WriteMessage(sender, new byte[0]);
                });
                writer.Start();

                Assert.AreEqual(longText, Encoding.UTF8.GetString(NetworkStreamReader.ReceiveMessage(receiver)));
                Assert.AreEqual(0, NetworkStreamReader.ReceiveMessage(receiver).Length, "続けて送った次のメッセージを読み過ぎない");
                writer.Join();
            }
        }

        [TestMethod]
        public void メッセージの途中で相手が切断したらnull()
        {
            using (LoopbackTcp tcp = LoopbackTcp.Connect())
            {
                NetworkStream receiver = tcp.ServerSide.GetStream();
                receiver.ReadTimeout = 5000;

                Write(tcp.ClientSide.GetStream(), "10\nabc");
                tcp.ClientSide.Client.Shutdown(SocketShutdown.Send);

                Assert.IsNull(NetworkStreamReader.ReceiveMessage(receiver));
            }
        }

        [TestMethod]
        public void メッセージの先頭にバイト数が無ければIOException()
        {
            using (LoopbackTcp tcp = LoopbackTcp.Connect())
            {
                NetworkStream receiver = tcp.ServerSide.GetStream();
                receiver.ReadTimeout = 5000;

                Write(tcp.ClientSide.GetStream(), "hello\n");

                Assert.ThrowsException<IOException>(() => NetworkStreamReader.ReceiveMessage(receiver));
            }
        }
    }
}
