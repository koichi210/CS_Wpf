using System;
using System.Windows.Media;

namespace WebCamera
{
    /// <summary>
    /// カメラから取った1フレーム分の画素を、OpenCvSharp の Mat から切り離して持つ入れ物。
    /// ワーカーは Mat のバッファを次の Read で上書きするので、UIへはこのコピーを渡す。
    /// 渡した後はワーカーもUIも中身を書き換えない(=スレッド間で共有しても安全)。
    /// </summary>
    internal sealed class CameraFrame
    {
        public CameraFrame(int width, int height, int stride, PixelFormat format, byte[] pixels)
        {
            if (pixels == null)
            {
                throw new ArgumentNullException(nameof(pixels));
            }
            if (width <= 0 || height <= 0 || stride < width * ((format.BitsPerPixel + 7) / 8) || pixels.Length < stride * height)
            {
                throw new ArgumentException("フレームのサイズと画素データが合っていない");
            }
            Width = width;
            Height = height;
            Stride = stride;
            Format = format;
            Pixels = pixels;
        }

        public int Width { get; }

        public int Height { get; }

        // 1行あたりのバイト数
        public int Stride { get; }

        public PixelFormat Format { get; }

        public byte[] Pixels { get; }
    }
}
