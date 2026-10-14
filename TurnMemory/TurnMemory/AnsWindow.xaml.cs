using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace TurnMemory
{
    /// <summary>
    /// 解答画面(MFC版AnsDlg)。正解の順番を表示し、入力と一致しなかったマスは赤字にする。
    /// </summary>
    public partial class AnsWindow : Window
    {
        // MFC版の解答ダイアログは全体が出題画面より7DLU下にずれている
        private const int _gridOffsetY = 7;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_DLGMODALFRAME = 0x00000001;
        private const int WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private readonly Logic _logic;
        private readonly CellGrid _grid;

        internal AnsWindow(Logic logic)
        {
            InitializeComponent();

            _logic = logic;
            _grid = new CellGrid(canvas, _gridOffsetY, CreateCell);
            _grid.Show(_logic.Size);
            CheckProc();
        }

        internal TextBlock CellText(int row, int col)
        {
            return (TextBlock)((Border)_grid.Cell(row, col)).Child;
        }

        // 正解の順番を表示し、入力と一致しないマスは赤字にする
        private void CheckProc()
        {
            int size = _logic.Size;
            for (int i = 0; i < size * size; i++)
            {
                TextBlock text = CellText(i / size, i % size);
                text.Text = _logic.GetAnswer(i).ToString(CultureInfo.InvariantCulture);
                if (!_logic.IsCorrect(i))
                {
                    text.Foreground = Brushes.Red;
                }
            }

            textTitle.Text = _logic.JudgeTitle();
        }

        private void buttonOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        // MFC版の解答ダイアログ(DS_MODALFRAME)はタイトルバーにアイコンが無かったので、WPFでも出さない。
        // WPFはIconを指定しなくてもexeのアイコンを出すため、WS_EX_DLGMODALFRAMEを付けて枠を作り直す
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_DLGMODALFRAME);
            SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_SMALL), IntPtr.Zero);
            SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_BIG), IntPtr.Zero);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }

        // MFC版のCTEXT(WS_BORDER)相当の、枠付きで中央寄せの数字表示
        private static FrameworkElement CreateCell()
        {
            return new Border
            {
                BorderBrush = SystemColors.WindowFrameBrush,
                BorderThickness = new Thickness(1),
                Child = new TextBlock { TextAlignment = TextAlignment.Center },
            };
        }
    }
}
