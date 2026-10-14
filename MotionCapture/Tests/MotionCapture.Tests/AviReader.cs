using System;
using System.Runtime.InteropServices;

namespace MotionCapture.Tests
{
    /// <summary>
    /// テストで作ったAVIファイルを、VfW(avifil32.dll)で読み戻して中身を確かめるためのヘルパー。
    /// </summary>
    internal sealed class AviInfo
    {
        public uint Rate;
        public uint Scale;
        public int Length;
        public uint FccHandler;
        public int Width;
        public int Height;
        public int BitCount;
    }

    internal static class AviReader
    {
        private const uint OF_READ = 0x00000000;
        private const uint OF_SHARE_DENY_WRITE = 0x00000020;

        [DllImport("avifil32.dll")]
        private static extern void AVIFileInit();

        [DllImport("avifil32.dll")]
        private static extern void AVIFileExit();

        [DllImport("avifil32.dll", CharSet = CharSet.Unicode, EntryPoint = "AVIFileOpenW")]
        private static extern int AVIFileOpen(out IntPtr ppfile, String szFile, uint uMode, IntPtr lpHandler);

        [DllImport("avifil32.dll")]
        private static extern int AVIFileGetStream(IntPtr pfile, out IntPtr ppavi, uint fccType, int lParam);

        [DllImport("avifil32.dll", EntryPoint = "AVIStreamInfoW")]
        private static extern int AVIStreamInfo(IntPtr pavi, ref NativeMethods.AVISTREAMINFOW psi, int lSize);

        [DllImport("avifil32.dll")]
        private static extern int AVIStreamLength(IntPtr pavi);

        [DllImport("avifil32.dll")]
        private static extern int AVIStreamReadFormat(IntPtr pavi, int lPos, ref NativeMethods.BITMAPINFO lpFormat, ref int lpcbFormat);

        [DllImport("avifil32.dll")]
        private static extern int AVIStreamRelease(IntPtr pavi);

        [DllImport("avifil32.dll")]
        private static extern int AVIFileRelease(IntPtr pfile);

        public static AviInfo Read(String path)
        {
            AVIFileInit();
            IntPtr file = IntPtr.Zero;
            IntPtr stream = IntPtr.Zero;
            try
            {
                if (AVIFileOpen(out file, path, OF_READ | OF_SHARE_DENY_WRITE, IntPtr.Zero) != 0)
                {
                    throw new InvalidOperationException("AVIFileOpen failed: " + path);
                }
                if (AVIFileGetStream(file, out stream, NativeMethods.streamtypeVIDEO, 0) != 0)
                {
                    throw new InvalidOperationException("AVIFileGetStream failed: " + path);
                }

                NativeMethods.AVISTREAMINFOW info = new NativeMethods.AVISTREAMINFOW();
                AVIStreamInfo(stream, ref info, Marshal.SizeOf(typeof(NativeMethods.AVISTREAMINFOW)));

                NativeMethods.BITMAPINFO format = new NativeMethods.BITMAPINFO();
                int formatSize = Marshal.SizeOf(typeof(NativeMethods.BITMAPINFO));
                AVIStreamReadFormat(stream, 0, ref format, ref formatSize);

                return new AviInfo
                {
                    Rate = info.dwRate,
                    Scale = info.dwScale,
                    Length = AVIStreamLength(stream),
                    FccHandler = info.fccHandler,
                    Width = format.bmiHeader.biWidth,
                    Height = format.bmiHeader.biHeight,
                    BitCount = format.bmiHeader.biBitCount,
                };
            }
            finally
            {
                if (stream != IntPtr.Zero)
                {
                    AVIStreamRelease(stream);
                }
                if (file != IntPtr.Zero)
                {
                    AVIFileRelease(file);
                }
                AVIFileExit();
            }
        }
    }
}
