using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using static MotionCapture.NativeMethods;

namespace MotionCapture
{
    /// <summary>
    /// MFC版SampleCaptAreaDlg（記録領域の確認ダイアログ）のWPF版。
    /// 開いた時点の画面から記録領域を撮って、縦横比を保ったまま700×400に収まるよう縮小して表示する。
    /// </summary>
    public partial class SampleCaptAreaWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_DLGMODALFRAME = 0x00000001;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const int WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        // 表示される前(コンストラクタ)に撮るので、このウィンドウ自身は写り込まない
        public SampleCaptAreaWindow(int x, int y, int width, int height)
        {
            InitializeComponent();

            Title = Logic.MakeSampleCaption(x, y, width, height);
            image_Sample.Source = CaptureArea(x, y, width, height);
        }

        // MFCのDS_MODALFRAMEダイアログと同じく、タイトルバーにアイコンを出さない
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_DLGMODALFRAME);
            SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_SMALL), IntPtr.Zero);
            SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_BIG), IntPtr.Zero);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }

        private void button_Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // 画面の記録領域を、縦横比を保ったまま表示枠に収まる大きさで撮る
        private static BitmapSource CaptureArea(int srcX, int srcY, int srcWidth, int srcHeight)
        {
            int width;
            int height;
            Logic.CalcSampleSize(srcWidth, srcHeight, out width, out height);

            IntPtr dcScreen = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(dcScreen);
            IntPtr bitmap = CreateCompatibleBitmap(dcScreen, width, height);
            IntPtr oldBitmap = SelectObject(memDc, bitmap);

            // 確認用なので、録画より時間をかけてきれいに縮める
            SetStretchBltMode(memDc, HALFTONE);
            SetBrushOrgEx(memDc, 0, 0, IntPtr.Zero);
            StretchBlt(memDc, 0, 0, width, height, dcScreen, srcX, srcY, srcWidth, srcHeight, SRCCOPY);

            SelectObject(memDc, oldBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, dcScreen);

            // WPFの画像に写し終えたら、元のビットマップはすぐ解放してよい
            try
            {
                BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                DeleteObject(bitmap);
            }
        }
    }
}
