using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace WebCamera
{
    /// <summary>
    /// プレビュー表示用の WriteableBitmap を持ち、届いたフレームの画素を書き込む(UIスレッド専用)。
    ///
    /// WinForms版はフレーム毎に新しい Bitmap を作って古いものを Dispose していた。
    /// WPFのビットマップは Dispose できず解放がGC任せなので、毎フレーム作り直すとメモリが膨らむ。
    /// そこでサイズと画素形式が同じ間は1枚を使い回し、画素だけ書き換える。
    /// 書き込むのはUIスレッドだけで、ワーカーは独立した CameraFrame を渡してくるので、
    /// 「ワーカーが書き込み中のバッファをUIが描画する」ことは起きない。
    /// </summary>
    internal sealed class PreviewSurface
    {
        public WriteableBitmap Bitmap { get; private set; }

        /// <summary>
        /// フレームを書き込む。Bitmap を作り直した(Image.Source の差し替えが要る)ときは true を返す。
        /// </summary>
        public bool Show(CameraFrame frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            bool recreated = false;
            if (Bitmap == null || Bitmap.PixelWidth != frame.Width || Bitmap.PixelHeight != frame.Height
                || Bitmap.Format != frame.Format)
            {
                Bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, frame.Format, null);
                recreated = true;
            }
            Bitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), frame.Pixels, frame.Stride, 0);
            return recreated;
        }

        /// <summary>
        /// 表示中の映像を複製する。表示中の Bitmap は次のフレームで上書きされるので、
        /// 押した瞬間の映像を保存したいときはこの複製を使う。映像が無ければ null。
        /// </summary>
        public BitmapSource CreateSnapshot()
        {
            if (Bitmap == null)
            {
                return null;
            }
            WriteableBitmap copy = Bitmap.Clone();
            copy.Freeze();
            return copy;
        }

        public void Clear()
        {
            Bitmap = null;
        }
    }
}
