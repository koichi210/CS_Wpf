using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace ChotChotChat
{
    // Client.Send / Server.Receive で共通の送受信(WinForms版と同じ)
    internal static class NetworkStreamReader
    {
        private const int _bufferSize = 256;
        // メッセージのバイト数の桁数の上限(これより長ければ壊れたデータとみなす)
        private const int _maxLengthDigits = 9;

        // 末尾が'\n'のデータを受け取るまで受信を続け、受信したバイト列を返す(サーバーの応答「文字数\n」用)。
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

        // メッセージを「バイト数\n」+本文 の形で送る。
        // 本文の途中に改行があっても、受け取る側がどこまでが1件か分かるようにするため
        // (以前は「最後が\nで、まだ届いていないデータが無い」で終わりとみなしていて、途中の\nで分かれて届くと切れた)
        public static void WriteMessage(NetworkStream ns, byte[] body)
        {
            byte[] header = Encoding.ASCII.GetBytes(body.Length.ToString(CultureInfo.InvariantCulture) + "\n");
            ns.Write(header, 0, header.Length);
            ns.Write(body, 0, body.Length);
        }

        // WriteMessageで送られたメッセージを1件受信し、本文のバイト列を返す。
        // 途中で相手が切断した場合はnull、先頭のバイト数が読めなければIOException(通信エラーと同じ扱い)
        public static byte[] ReceiveMessage(NetworkStream ns)
        {
            // バイト数の行は、本文を読み過ぎないよう1バイトずつ読む
            int length = 0;
            int digits = 0;
            while (true)
            {
                int b = ns.ReadByte();
                if (b < 0)
                {
                    return null;
                }
                if (b == '\n')
                {
                    break;
                }
                if (b < '0' || b > '9' || digits >= _maxLengthDigits)
                {
                    throw new IOException("メッセージの長さが読み取れません");
                }
                length = length * 10 + (b - '0');
                digits++;
            }
            if (digits == 0)
            {
                throw new IOException("メッセージの長さが読み取れません");
            }

            // 本文はバイト数の分だけ、分かれて届いてもつなげて受け取る
            byte[] body = new byte[length];
            int received = 0;
            while (received < length)
            {
                int size = ns.Read(body, received, length - received);
                if (size == 0)
                {
                    return null;
                }
                received += size;
            }
            return body;
        }
    }
}
