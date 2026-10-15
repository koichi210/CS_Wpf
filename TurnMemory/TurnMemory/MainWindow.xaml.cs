using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace TurnMemory
{
    /// <summary>
    /// 出題画面(MFC版TurnMemoryDlg)。
    /// 「スタート」で1秒ごとにマスへ順番を1つずつ表示し、全部出したら5秒のカウントダウンの後、
    /// マスを空にして順番の入力を受け付ける。「御明算」で解答画面(AnsWindow)を開いて答え合わせする。
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int _showIntervalMs = 1000;   // 順番を1つ表示する間隔
        private const int _waitIntervalMs = 1000;   // 記憶時間のカウントダウン間隔

        private readonly Logic _logic;
        private readonly CellGrid _grid;
        private readonly DispatcherTimer _showTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_showIntervalMs) };
        private readonly DispatcherTimer _waitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_waitIntervalMs) };

        public MainWindow()
            : this(new Random())
        {
        }

        // テストから乱数を差し替えて出題順を固定するための入口
        internal MainWindow(Random random)
        {
            InitializeComponent();

            _logic = new Logic(random);
            _grid = new CellGrid(canvas, 0, CreateCell);
            _showTimer.Tick += (s, e) => ShowProc();
            _waitTimer.Tick += (s, e) => WaitProc();

            textTitle.Text = Logic.StartStr;

            foreach (string choice in Logic.SizeChoices())
            {
                comboBoxProblem.Items.Add(choice);
            }
            comboBoxProblem.SelectedIndex = Logic.SizeToIndex(Logic.DefaultSize);
            Refresh();

            // MFC版はダイアログの先頭のコントロール(マス数の選択)に初期フォーカスがあった
            Loaded += (s, e) => comboBoxProblem.Focus();
        }

        internal Logic GameLogic
        {
            get { return _logic; }
        }

        internal TextBox Cell(int row, int col)
        {
            return (TextBox)_grid.Cell(row, col);
        }

        internal CellGrid Cells
        {
            get { return _grid; }
        }

        // 出題中は「ストップ」として働き、出題を打ち切る
        private void buttonStart_Click(object sender, RoutedEventArgs e)
        {
            if (_logic.State == GameState.Start)
            {
                _logic.Stop();
                InitProc();
                ExitProc();
                return;
            }

            buttonStart.Content = Logic.StopButton;
            textTitle.Text = Logic.KeepMindStr;
            buttonAns.IsEnabled = false;
            comboBoxProblem.IsEnabled = false;

            Refresh();
            _logic.Start();
            _showTimer.Start();
        }

        private void buttonAns_Click(object sender, RoutedEventArgs e)
        {
            int size = _logic.Size;
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    _logic.SetInput(i * size + j, Logic.ParseInput(Cell(i, j).Text));
                }
            }

            AnsWindow dialog = new AnsWindow(_logic) { Owner = this };
            dialog.ShowDialog();

            textTitle.Text = Logic.StartStr;
        }

        private void buttonOk_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _showTimer.Stop();
            _waitTimer.Stop();
        }

        // 順番表示タイマ。全部の順番を表示し終えたら、記憶時間のカウントダウンに移る
        internal void ShowProc()
        {
            bool finished;
            int index = _logic.ShowNext(out finished);
            if (index >= 0)
            {
                int size = _logic.Size;
                Cell(index / size, index % size).Text = _logic.GetAnswer(index).ToString(CultureInfo.InvariantCulture);
            }

            if (finished)
            {
                _showTimer.Stop();
                _waitTimer.Start();
            }
        }

        // 記憶時間タイマ。0秒まで表示したら解答の入力を受け付ける
        internal void WaitProc()
        {
            bool finished;
            int seconds = _logic.WaitTick(out finished);
            textTitle.Text = Logic.RememberText(seconds);

            if (finished)
            {
                InitProc();
                _waitTimer.Stop();
            }
        }

        private void ExitProc()
        {
            _logic.Exit();
            textTitle.Text = Logic.StartStr;
            buttonStart.Content = Logic.StartButton;
            _showTimer.Stop();
            _waitTimer.Stop();
        }

        // 解答の入力を受け付ける状態にする(入力は出題開始時にクリア済み)。
        // ストップで打ち切ったときは State が Stop なので「御明算」は押せないまま
        private void InitProc()
        {
            buttonStart.Content = Logic.StartButton;
            textTitle.Text = Logic.EndStr;
            comboBoxProblem.IsEnabled = true;
            buttonAns.IsEnabled = _logic.State == GameState.End;

            ResetCells(true);
        }

        // 選択中のマス数に合わせてマスを表示し直す。
        // MFC版と同じく、マス数を選び直しただけでは表示は変わらず、次の「スタート」で反映される
        private void Refresh()
        {
            _logic.Size = Logic.IndexToSize(comboBoxProblem.SelectedIndex);

            _grid.Show(_logic.Size);
            ResetCells(false);
        }

        // 表示中のマスを空にし、入力の可否を切り替える
        private void ResetCells(bool enable)
        {
            int size = _logic.Size;
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    TextBox cell = Cell(i, j);
                    cell.IsEnabled = enable;
                    cell.Text = "";
                }
            }
        }

        // MFC版のEDITTEXT(ES_CENTER | ES_NUMBER)相当の、数字だけ入力できる中央寄せのマス
        private static FrameworkElement CreateCell()
        {
            TextBox cell = new TextBox
            {
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0),
            };
            // 全角数字などがIMEから入ってこないよう、IMEは使わせない
            InputMethod.SetIsInputMethodEnabled(cell, false);
            cell.PreviewTextInput += (s, e) => e.Handled = !IsDigits(e.Text);
            // スペースはPreviewTextInputを通らずに入力されるので、キーで止める。
            // Enterは次のマスへ進める(MFC版はEnterで終了ボタンが押されて、入力中にアプリが閉じてしまっていた)
            cell.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Space)
                {
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter)
                {
                    ((TextBox)s).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    e.Handled = true;
                }
            };
            DataObject.AddPastingHandler(cell, (s, e) =>
            {
                string text = e.DataObject.GetDataPresent(DataFormats.UnicodeText)
                    ? e.DataObject.GetData(DataFormats.UnicodeText) as string
                    : null;
                if (!IsDigits(text))
                {
                    e.CancelCommand();
                }
            });
            return cell;
        }

        private static bool IsDigits(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return false;
            }
            foreach (char c in text)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }
            return true;
        }
    }
}
