using System;
using System.Runtime.InteropServices;
using System.Windows.Media;
using OpenCvSharp;

namespace WebCamera
{
    /// <summary>
    /// OpenCvSharp の Mat を、WPFの WriteableBitmap に書き込める CameraFrame へコピーする。
    /// WinForms版は OpenCvSharp.Extensions の BitmapConverter.ToBitmap(System.Drawing.Bitmap)を使っていたが、
    /// WPFでは System.Drawing を経由する理由が無いので、画素を直接コピーしている。
    /// </summary>
    internal static class FrameConverter
    {
        /// <summary>
        /// Mat の画素を独立した配列にコピーする。返した CameraFrame は Mat が上書き・破棄されても影響を受けない。
        /// カメラ(VideoCapture)の映像は通常 8bit の BGR 3チャンネル。念のため 1/4 チャンネルにも対応する。
        /// </summary>
        public static CameraFrame ToFrame(Mat mat)
        {
            if (mat == null)
            {
                throw new ArgumentNullException(nameof(mat));
            }
            if (mat.Empty())
            {
                throw new ArgumentException("空のフレームは変換できない", nameof(mat));
            }
            if (mat.Depth() != MatType.CV_8U)
            {
                throw new NotSupportedException("8bit以外の画素形式には対応していない: " + mat.Type());
            }

            int channels = mat.Channels();
            PixelFormat format = GetPixelFormat(channels);
            int width = mat.Width;
            int height = mat.Height;
            int rowBytes = width * channels;
            byte[] pixels = new byte[rowBytes * height];

            if (mat.IsContinuous())
            {
                Marshal.Copy(mat.Data, pixels, 0, pixels.Length);
            }
            else
            {
                // 切り出し(ROI)などで行の間に隙間がある Mat は、1行ずつ詰めてコピーする
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(mat.Ptr(y), pixels, y * rowBytes, rowBytes);
                }
            }

            return new CameraFrame(width, height, rowBytes, format, pixels);
        }

        // OpenCV の並び(BGR/BGRA)は WPF の Bgr24/Bgra32 と同じなので、並べ替えずにそのまま使える
        internal static PixelFormat GetPixelFormat(int channels)
        {
            switch (channels)
            {
                case 1:
                    return PixelFormats.Gray8;
                case 3:
                    return PixelFormats.Bgr24;
                case 4:
                    return PixelFormats.Bgra32;
                default:
                    throw new NotSupportedException("チャンネル数 " + channels + " の画像には対応していない");
            }
        }
    }
}
