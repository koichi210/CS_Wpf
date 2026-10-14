using System;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenCvSharp;

namespace WebCamera.Tests
{
    /// <summary>
    /// FrameConverter(Mat → WriteableBitmap用の画素配列)のテスト。
    /// カメラは使わず、メモリ上で作った Mat を変換する(OpenCvSharpのネイティブDLLは使う)。
    /// </summary>
    [TestClass]
    public class FrameConverterTests
    {
        [TestMethod]
        public void カラー3チャンネルはBgr24で詰めてコピーする()
        {
            using (Mat mat = new Mat(2, 3, MatType.CV_8UC3, new Scalar(10, 20, 30)))
            {
                mat.Set(1, 2, new Vec3b(1, 2, 3));

                CameraFrame frame = FrameConverter.ToFrame(mat);

                Assert.AreEqual(3, frame.Width);
                Assert.AreEqual(2, frame.Height);
                Assert.AreEqual(9, frame.Stride);
                Assert.AreEqual(PixelFormats.Bgr24, frame.Format);
                Assert.AreEqual(18, frame.Pixels.Length);
                // OpenCVのBGRの並びのまま
                CollectionAssert.AreEqual(new byte[] { 10, 20, 30 }, Slice(frame.Pixels, 0, 3));
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, Slice(frame.Pixels, 9 + 6, 3));
            }
        }

        [TestMethod]
        public void グレー1チャンネルはGray8()
        {
            using (Mat mat = new Mat(2, 2, MatType.CV_8UC1, new Scalar(77)))
            {
                CameraFrame frame = FrameConverter.ToFrame(mat);

                Assert.AreEqual(PixelFormats.Gray8, frame.Format);
                Assert.AreEqual(2, frame.Stride);
                CollectionAssert.AreEqual(new byte[] { 77, 77, 77, 77 }, frame.Pixels);
            }
        }

        [TestMethod]
        public void 透過付き4チャンネルはBgra32()
        {
            using (Mat mat = new Mat(1, 1, MatType.CV_8UC4, new Scalar(1, 2, 3, 4)))
            {
                CameraFrame frame = FrameConverter.ToFrame(mat);

                Assert.AreEqual(PixelFormats.Bgra32, frame.Format);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, frame.Pixels);
            }
        }

        [TestMethod]
        public void 切り出したMatは行の隙間を詰めてコピーする()
        {
            using (Mat mat = new Mat(3, 4, MatType.CV_8UC1, new Scalar(0)))
            {
                for (int y = 0; y < 3; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        mat.Set(y, x, (byte)(y * 10 + x));
                    }
                }

                using (Mat roi = new Mat(mat, new Rect(1, 1, 2, 2)))
                {
                    Assert.IsFalse(roi.IsContinuous(), "前提: 切り出しは行が連続していない");

                    CameraFrame frame = FrameConverter.ToFrame(roi);

                    Assert.AreEqual(2, frame.Stride);
                    CollectionAssert.AreEqual(new byte[] { 11, 12, 21, 22 }, frame.Pixels);
                }
            }
        }

        [TestMethod]
        public void 変換後にMatを書き換えてもフレームは変わらない()
        {
            CameraFrame frame;
            using (Mat mat = new Mat(1, 1, MatType.CV_8UC3, new Scalar(5, 6, 7)))
            {
                frame = FrameConverter.ToFrame(mat);
                mat.SetTo(new Scalar(0, 0, 0));
            }

            CollectionAssert.AreEqual(new byte[] { 5, 6, 7 }, frame.Pixels);
        }

        [TestMethod]
        public void 空のMatや8bit以外は変換しない()
        {
            using (Mat empty = new Mat())
            using (Mat depth16 = new Mat(1, 1, MatType.CV_16UC3))
            using (Mat twoChannels = new Mat(1, 1, MatType.CV_8UC2))
            {
                Assert.ThrowsException<ArgumentException>(() => FrameConverter.ToFrame(empty));
                Assert.ThrowsException<NotSupportedException>(() => FrameConverter.ToFrame(depth16));
                Assert.ThrowsException<NotSupportedException>(() => FrameConverter.ToFrame(twoChannels));
            }
            Assert.ThrowsException<ArgumentNullException>(() => FrameConverter.ToFrame(null));
        }

        private static byte[] Slice(byte[] source, int start, int length)
        {
            byte[] result = new byte[length];
            Array.Copy(source, start, result, 0, length);
            return result;
        }
    }
}
