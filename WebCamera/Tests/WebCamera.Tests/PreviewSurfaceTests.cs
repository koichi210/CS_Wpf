using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StandardTemplate.Wpf.Tests;

namespace WebCamera.Tests
{
    /// <summary>
    /// PreviewSurface(プレビュー用 WriteableBitmap の使い回しとスナップショットの複製)のテスト。
    /// WriteableBitmap は作ったスレッドでしか触れないので、STAスレッド上で実行する。
    /// </summary>
    [TestClass]
    public class PreviewSurfaceTests
    {
        private static CameraFrame CreateFrame(int width, int height, byte value)
        {
            byte[] pixels = new byte[width * 3 * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = value;
            }
            return new CameraFrame(width, height, width * 3, PixelFormats.Bgr24, pixels);
        }

        private static byte FirstByte(BitmapSource bitmap)
        {
            int stride = (bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8;
            byte[] pixels = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, stride, 0);
            return pixels[0];
        }

        [TestMethod]
        public void 最初のフレームでBitmapを作る()
        {
            StaRunner.Run(() =>
            {
                PreviewSurface surface = new PreviewSurface();
                Assert.IsNull(surface.Bitmap);
                Assert.IsNull(surface.CreateSnapshot(), "映像が無ければ複製も無い");

                Assert.IsTrue(surface.Show(CreateFrame(4, 3, 50)));

                Assert.AreEqual(4, surface.Bitmap.PixelWidth);
                Assert.AreEqual(3, surface.Bitmap.PixelHeight);
                Assert.AreEqual(PixelFormats.Bgr24, surface.Bitmap.Format);
                Assert.AreEqual(50, FirstByte(surface.Bitmap));
            });
        }

        [TestMethod]
        public void 同じサイズのフレームはBitmapを使い回して画素だけ書き換える()
        {
            StaRunner.Run(() =>
            {
                PreviewSurface surface = new PreviewSurface();
                surface.Show(CreateFrame(4, 3, 50));
                WriteableBitmap first = surface.Bitmap;

                Assert.IsFalse(surface.Show(CreateFrame(4, 3, 99)));

                Assert.AreSame(first, surface.Bitmap);
                Assert.AreEqual(99, FirstByte(surface.Bitmap));
            });
        }

        [TestMethod]
        public void サイズが変わったらBitmapを作り直す()
        {
            StaRunner.Run(() =>
            {
                PreviewSurface surface = new PreviewSurface();
                surface.Show(CreateFrame(4, 3, 50));
                WriteableBitmap first = surface.Bitmap;

                Assert.IsTrue(surface.Show(CreateFrame(8, 6, 50)));

                Assert.AreNotSame(first, surface.Bitmap);
                Assert.AreEqual(8, surface.Bitmap.PixelWidth);
            });
        }

        [TestMethod]
        public void スナップショットは後のフレームで上書きされない()
        {
            StaRunner.Run(() =>
            {
                PreviewSurface surface = new PreviewSurface();
                surface.Show(CreateFrame(4, 3, 50));

                BitmapSource snapshot = surface.CreateSnapshot();
                surface.Show(CreateFrame(4, 3, 99));

                Assert.IsTrue(snapshot.IsFrozen, "保存処理へ渡せるよう凍結しておく");
                Assert.AreEqual(50, FirstByte(snapshot));
                Assert.AreEqual(99, FirstByte(surface.Bitmap));
            });
        }

        [TestMethod]
        public void Clearで映像を手放す()
        {
            StaRunner.Run(() =>
            {
                PreviewSurface surface = new PreviewSurface();
                surface.Show(CreateFrame(4, 3, 50));

                surface.Clear();

                Assert.IsNull(surface.Bitmap);
            });
        }
    }
}
