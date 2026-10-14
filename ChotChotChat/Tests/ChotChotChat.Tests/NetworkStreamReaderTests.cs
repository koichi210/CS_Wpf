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
    }
}
