using System.IO;
using System.Net.Sockets;

namespace ChotChotChat
{
    // Client.Send / Server.Receive で共通の受信ループ(WinForms版と同じ)
    internal static class NetworkStreamReader
    {
        private const int _bufferSize = 256;

        // 末尾が'\n'のデータを受け取るまで受信を続け、受信したバイト列を返す。
        // 途中で相手が切断した(Readが0を返した)場合はnullを返す
        public static byte[] ReceiveLine(NetworkStream ns)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] resBytes = new byte[_bufferSize];
                int resSize;

                do
                {
                    // データの一部を受信する
                    resSize = ns.Read(resBytes, 0, resBytes.Length);
                    if (resSize == 0)
                    {
                        return null;
                    }

                    // 受信したデータを蓄積し、データの最後が\nでない時は受信を続ける
                    ms.Write(resBytes, 0, resSize);
                } while (ns.DataAvailable || resBytes[resSize - 1] != '\n');

                return ms.ToArray();
            }
        }
    }
}
