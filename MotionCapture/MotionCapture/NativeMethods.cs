using System;
using System.Runtime.InteropServices;

namespace MotionCapture
{
    /// <summary>
    /// MFC版manage_avi.cc / sample_capt_area_dlg.cc が使っていたWin32 API（GDI・VfW）の宣言。
    /// 構造体・定数名はWindows SDKの表記のまま。
    /// </summary>
    internal static class NativeMethods
    {
        // ---- 構造体 ----

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        // C++のBITMAPINFOと同じく、カラーテーブルはRGBQUAD 1つ分だけ持つ(sizeof = 44)
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public uint bmiColors;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct AVISTREAMINFOW
        {
            public uint fccType;
            public uint fccHandler;
            public uint dwFlags;
            public uint dwCaps;
            public ushort wPriority;
            public ushort wLanguage;
            public uint dwScale;
            public uint dwRate;
            public uint dwStart;
            public uint dwLength;
            public uint dwInitialFrames;
            public uint dwSuggestedBufferSize;
            public uint dwQuality;
            public uint dwSampleSize;
            public RECT rcFrame;
            public uint dwEditCount;
            public uint dwFormatChangeCount;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public String szName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct AVICOMPRESSOPTIONS
        {
            public uint fccType;
            public uint fccHandler;
            public uint dwKeyFrameEvery;
            public uint dwQuality;
            public uint dwBytesPerSecond;
            public uint dwFlags;
            public IntPtr lpFormat;
            public uint cbFormat;
            public IntPtr lpParms;
            public uint cbParms;
            public uint dwInterleaveEvery;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct COMPVARS
        {
            public int cbSize;
            public uint dwFlags;
            public IntPtr hic;
            public uint fccType;
            public uint fccHandler;
            public IntPtr lpbiIn;
            public IntPtr lpbiOut;
            public IntPtr lpBitsOut;
            public IntPtr lpBitsPrev;
            public int lFrame;
            public int lKey;
            public int lDataRate;
            public int lQ;
            public int lKeyCount;
            public IntPtr lpState;
            public int cbState;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ICONINFO
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        // ---- 定数 ----

        public const int AVIERR_OK = 0;
        public const uint OF_WRITE = 0x00000001;
        public const uint OF_CREATE = 0x00001000;
        public const uint AVIIF_KEYFRAME = 0x00000010;
        public const uint AVICOMPRESSF_DATARATE = 0x00000002;
        public const uint AVICOMPRESSF_KEYFRAMES = 0x00000004;
        public const uint ICMF_COMPVARS_VALID = 0x00000001;
        public const uint ICMF_CHOOSE_KEYFRAME = 0x00000001;
        public const uint ICMF_CHOOSE_DATARATE = 0x00000002;
        public const int ICQUALITY_DEFAULT = -1;
        public const uint BI_RGB = 0;
        public const uint DIB_RGB_COLORS = 0;
        public const uint SRCCOPY = 0x00CC0020;
        public const int COLORONCOLOR = 3;
        public const int HALFTONE = 4;
        public const int CURSOR_SHOWING = 0x00000001;

        public static readonly uint streamtypeVIDEO = MakeFourCC('v', 'i', 'd', 's');
        public static readonly uint comptypeDIB = MakeFourCC('D', 'I', 'B', ' ');

        // mmioFOURCC
        public static uint MakeFourCC(char c0, char c1, char c2, char c3)
        {
            return (uint)(byte)c0 | ((uint)(byte)c1 << 8) | ((uint)(byte)c2 << 16) | ((uint)(byte)c3 << 24);
        }

        // ---- avifil32.dll ----

        [DllImport("avifil32.dll")]
        public static extern void AVIFileInit();

        [DllImport("avifil32.dll")]
        public static extern void AVIFileExit();

        [DllImport("avifil32.dll", CharSet = CharSet.Unicode, EntryPoint = "AVIFileOpenW")]
        public static extern int AVIFileOpen(out IntPtr ppfile, String szFile, uint uMode, IntPtr lpHandler);

        [DllImport("avifil32.dll", EntryPoint = "AVIFileCreateStreamW")]
        public static extern int AVIFileCreateStream(IntPtr pfile, out IntPtr ppavi, ref AVISTREAMINFOW psi);

        [DllImport("avifil32.dll")]
        public static extern int AVIStreamSetFormat(IntPtr pavi, int lPos, ref BITMAPINFO lpFormat, int cbFormat);

        [DllImport("avifil32.dll")]
        public static extern int AVIMakeCompressedStream(out IntPtr ppsCompressed, IntPtr ppsSource, ref AVICOMPRESSOPTIONS lpOptions, IntPtr pclsidHandler);

        [DllImport("avifil32.dll")]
        public static extern int AVIStreamWrite(IntPtr pavi, int lStart, int lSamples, IntPtr lpBuffer, int cbBuffer, uint dwFlags, IntPtr plSampWritten, IntPtr plBytesWritten);

        [DllImport("avifil32.dll")]
        public static extern int AVIStreamRelease(IntPtr pavi);

        [DllImport("avifil32.dll")]
        public static extern int AVIFileRelease(IntPtr pfile);

        // ---- msvfw32.dll ----

        [DllImport("msvfw32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ICCompressorChoose(IntPtr hwnd, uint uiFlags, ref BITMAPINFO pvIn, IntPtr lpData, ref COMPVARS pc, IntPtr lpszTitle);

        [DllImport("msvfw32.dll")]
        public static extern void ICCompressorFree(ref COMPVARS pc);

        // ---- gdi32.dll ----

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateDC(String lpszDriver, String lpszDevice, String lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr ho);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern int SetStretchBltMode(IntPtr hdc, int mode);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr lppt);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, uint rop);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool StretchBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
            IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, uint rop);

        // ---- user32.dll ----

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorInfo(ref CURSORINFO pci);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DrawIcon(IntPtr hDC, int x, int y, IntPtr hIcon);

        // ---- 圧縮の選択ダイアログをキャンセルで閉じるのに使う ----

        public const uint WM_COMMAND = 0x0111;
        public const int IDCANCEL = 2;

        [return: MarshalAs(UnmanagedType.Bool)]
        public delegate bool EnumThreadWndProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadWndProc lpfn, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentProcessId();
    }
}
