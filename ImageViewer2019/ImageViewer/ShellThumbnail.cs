using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ImageViewer
{
    // Windowsのシェル(エクスプローラーと同じ仕組み)からファイルのサムネイルを取り出す。
    // 動画はBitmapImageで読めないため、一覧のサムネイルはシェルが作る先頭付近のフレームで代用する
    // (WinForms版ImageViewerのShellThumbnail.csをWPF向けにしたもの。違いはBitmapSourceで返すことだけ)
    // (外部dllやNuGetを足さずに済むよう、IShellItemImageFactoryを直接P/Invokeで呼んでいる)
    public static class ShellThumbnail
    {
        [Flags]
        private enum SiigbfFlags
        {
            ResizeToFit = 0x00,
            BiggerSizeOk = 0x01,
            MemoryOnly = 0x02,
            IconOnly = 0x04,
            ThumbnailOnly = 0x08,
            InCacheOnly = 0x10,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize
        {
            public int Width;
            public int Height;
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(NativeSize size, SiigbfFlags flags, out IntPtr phbm);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] String path,
            IntPtr pbc,
            [In] ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        // サムネイルを取り出す。取れないとき(コーデックが無い・壊れた動画・ファイルが消えた等)はnullを返す。
        // アイコンで代用すると画像の一覧に紛れて分かりにくいので、本物のサムネイルだけを対象にする(ThumbnailOnly)
        public static BitmapSource GetThumbnail(String filePath, int width, int height)
        {
            if (String.IsNullOrEmpty(filePath))
            {
                return null;
            }

            IShellItemImageFactory factory = null;
            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                Guid iid = typeof(IShellItemImageFactory).GUID;
                if (SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref iid, out factory) != 0 || factory == null)
                {
                    return null;
                }

                NativeSize size = new NativeSize { Width = Math.Max(1, width), Height = Math.Max(1, height) };
                // ResizeToFit: 縦横比を保ったまま指定サイズに収めた画像をシェル側で作ってもらう
                // (BiggerSizeOkだとキャッシュ済みの大きな画像がそのまま返り、無駄にメモリを使う)
                if (factory.GetImage(size, SiigbfFlags.ResizeToFit | SiigbfFlags.ThumbnailOnly, out hBitmap) != 0
                    || hBitmap == IntPtr.Zero)
                {
                    return null;
                }

                // CreateBitmapSourceFromHBitmapは中身をコピーした別の画像を作るので、元のHBITMAPはfinallyで解放してよい。
                // 読み込み用のスレッドで作ってUIスレッドへ渡すのでFreezeしておく
                BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            catch (Exception ex) when (ex is ExternalException || ex is ArgumentException
                || ex is InvalidCastException || ex is OutOfMemoryException)
            {
                // COMの呼び出し失敗(COMExceptionはExternalExceptionの派生)・GDI+の変換失敗は「取れなかった」扱い
                return null;
            }
            finally
            {
                if (hBitmap != IntPtr.Zero)
                {
                    DeleteObject(hBitmap);
                }
                if (factory != null)
                {
                    Marshal.ReleaseComObject(factory);
                }
            }
        }
    }
}
