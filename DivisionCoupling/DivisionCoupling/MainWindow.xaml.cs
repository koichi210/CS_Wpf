using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DivisionCoupling
{
    /// <summary>
    /// MFC版DivisionCouplingDlg（ファイルの分割と結合）のWPF版。
    /// 分割・結合はワーカースレッドで行い、終わったことだけをUIスレッドへ伝えて結果を表示する。
    /// </summary>
    public partial class MainWindow : Window
    {
        // MFC版のCWnd::MessageBoxは、キャプション省略時にアプリ名(exe名)を出す
        private const String _appName = "DivisionCoupling";

        private int _divSize = 0;                           // 分割サイズ(バイト)
        private String _srcPath = String.Empty;             // 分割元ファイル / 結合で選んだ分割ファイル
        private String _destPath = String.Empty;            // 出力中のファイル（エラー表示にも使う）
        private ProcessError _error = ProcessError.None;
        private volatile Boolean _running = false;          // 「停止」でワーカースレッドに中断を伝える
        private volatile Boolean _closing = false;          // 終了処理中（結果のメッセージを出さない）
        private Thread _worker;

        // ファイル選択ダイアログ（選んだパス、キャンセルなら null を返す）。テストでは差し替えて、ダイアログを出さずに確認する
        internal Func<Window, String> ShowOpenFileDialog { get; set; } = FileOpenDialog.Show;

        public MainWindow()
        {
            InitializeComponent();

            // ES_NUMBER相当: 数字以外は貼り付けさせない
            DataObject.AddPastingHandler(textBox_Size, textBox_Size_Pasting);

            // シャットダウン・ログオフでは、この後すぐプロセスごと終了させられるので、確認せずに止めて後片付けさせる
            if (Application.Current != null)
            {
                Application.Current.SessionEnding += (s, e) => StopWorkers();
            }
        }

        // ダイアログと同じく、最初の入力欄にフォーカスを置いておく
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            textBox_SplitFileName.Focus();
        }

        private void button_Split_Click(object sender, RoutedEventArgs e)
        {
            _srcPath = textBox_SplitFileName.Text;

            // KB → バイト。int に収まらない値は分割サイズエラーにする
            _divSize = Logic.ToDivSize(Logic.ParseDivSizeKb(textBox_Size.Text));

            // 前回の分割ファイルが残っていると、今回の個数が前回より少ないときに後ろの番号が残り、
            // 結合で古いデータまでつながってしまう。1つ目を作る前に、同じ名前の分割ファイルを消すか確認する
            // (元ファイルが無い・分割サイズが不正なときは、これまでどおり分割処理のエラー表示に任せる)
            if (_divSize > 0 && File.Exists(_srcPath))
            {
                List<String> oldParts = Logic.FindPartFiles(_srcPath);
                if (oldParts.Count > 0)
                {
                    String found = (oldParts.Count == 1) ? oldParts[0]
                        : oldParts[0] + "\n～ " + Path.GetFileName(oldParts[oldParts.Count - 1]) + " (" + oldParts.Count + "個)";
                    if (MessageBox.Show(this, "前回の分割ファイルが残っています。削除してから分割しますか？\n\n" + found, "Warning", MessageBoxButton.YesNo) == MessageBoxResult.No)
                    {
                        MessageBox.Show(this, "処理を中断しました。", _appName);
                        return;
                    }

                    String failedPath;
                    if (!Logic.TryDeleteFiles(oldParts, out failedPath))
                    {
                        MessageBox.Show(this, "前回の分割ファイルを削除できませんでした。\n\n" + failedPath, "error", MessageBoxButton.OK);
                        return;
                    }
                }
            }

            StartProcess(Split);
        }

        private void button_Merge_Click(object sender, RoutedEventArgs e)
        {
            _srcPath = textBox_MergeFileName.Text;
            _destPath = Logic.MakeMergeDestPath(_srcPath);

            // 確認はワーカーを動かす前に UI スレッドで行う
            // 1つ目の分割ファイルが無ければ、空のファイルを作って（既存なら空で上書きして）しまうので始めない
            String firstPart = Logic.MakePartPath(_destPath, 1);
            if (!Logic.PathExists(firstPart))
            {
                MessageBox.Show(this, "元ファイルオープンエラー\n\n" + firstPart, "error", MessageBoxButton.OK);
                return;
            }
            if (Logic.PathExists(_destPath))
            {
                if (MessageBox.Show(this, "すでにファイルが存在します。上書きしますか？\n" + _destPath, "Warning", MessageBoxButton.YesNo) == MessageBoxResult.No)
                {
                    MessageBox.Show(this, "処理を中断しました。", _appName);
                    return;
                }
            }

            StartProcess(Merge);
        }

        private void button_SplitBrowse_Click(object sender, RoutedEventArgs e)
        {
            BrowseFile(textBox_SplitFileName);
        }

        private void button_MergeBrowse_Click(object sender, RoutedEventArgs e)
        {
            BrowseFile(textBox_MergeFileName);
        }

        // 開始ボタンはワーカーが止まりきってから OnProcessFinished で戻す（前の処理と同時に動かさないため）
        private void button_Stop_Click(object sender, RoutedEventArgs e)
        {
            _running = false;
            button_Stop.IsEnabled = false;
        }

        // MFC版の「閉じる」(IDOK)とEsc(IDCANCEL)はどちらもダイアログを閉じるだけ。確認はClosingで行う
        private void button_Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!StopWorkersForClose())
            {
                e.Cancel = true;
            }
        }

        // 処理中なら確認のうえ中断し、終了を待つ（閉じてよければ true）
        private Boolean StopWorkersForClose()
        {
            if (IsWorkerRunning())
            {
                MessageBoxResult answer = MessageBox.Show(this, "処理中です。中止して閉じますか？\n\n作成途中のファイルは削除します。",
                    "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes)
                {
                    return false;
                }
            }

            StopWorkers();
            return true;
        }

        // ワーカーは作成途中のファイルを消してから終わるので、閉じる前に止めて終了を待つ（確認はしない）
        private void StopWorkers()
        {
            _closing = true;
            _running = false;
            IsEnabled = false;  // 待っている間に操作させない
            Thread worker = _worker;
            if (worker != null)
            {
                // ワーカーはUIスレッドへBeginInvokeで投げるだけ(待たない)ので、ここで待ってもデッドロックしない
                worker.Join();
            }
            IsEnabled = true;
        }

        private Boolean IsWorkerRunning()
        {
            Thread worker = _worker;
            return worker != null && worker.IsAlive;
        }

        private void StartProcess(Action process)
        {
            _error = ProcessError.None;
            _running = true;
            EnableControls(true);

            // ワーカーは画面に何も出さず、終わったことだけ UI スレッドに伝えてすぐ終わる
            _worker = new Thread(() =>
            {
                process();
                Dispatcher.BeginInvoke(new Action(OnProcessFinished));
            });
            _worker.IsBackground = true;
            _worker.Start();
        }

        // ワーカーの終了通知（UIスレッドで呼ばれる）
        private void OnProcessFinished()
        {
            _running = false;
            EnableControls(false);

            // 閉じる途中はウィンドウの終了を妨げないよう、何も表示しない
            if (_closing)
            {
                return;
            }

            String msg;
            switch (_error)
            {
                case ProcessError.None:
                    return;
                case ProcessError.Aborted:
                    MessageBox.Show(this, "処理を中止しました。\n作成途中のファイルは削除しました。", "info", MessageBoxButton.OK);
                    return;
                case ProcessError.OpenSource:
                    msg = "元ファイルオープンエラー";
                    break;
                case ProcessError.OpenDest:
                    msg = String.Format("ファイルが作成できません\n\n{0}", _destPath);
                    break;
                case ProcessError.Alloc:
                    msg = "Workメモリ確保エラー";
                    break;
                case ProcessError.DivSize:
                    msg = "分割サイズエラー";
                    break;
                default:
                    msg = String.Empty;
                    break;
            }
            MessageBox.Show(this, msg, "error", MessageBoxButton.OK);
        }

        // ワーカースレッドで動く
        private void Split()
        {
            String destPath;
            _error = Logic.Split(_srcPath, _divSize, () => _running,
                partCount => Dispatcher.BeginInvoke(new Action(() =>
                {
                    progressBar_SplitMerge.Minimum = 0;
                    progressBar_SplitMerge.Maximum = partCount;
                })),
                index => Dispatcher.BeginInvoke(new Action(() => progressBar_SplitMerge.Value = index)),
                out destPath);
            _destPath = destPath;
        }

        // ワーカースレッドで動く。結合先 _destPath は button_Merge_Click で決めて、上書きの確認も済んでいる
        private void Merge()
        {
            _error = Logic.Merge(_destPath, () => _running);
        }

        // MFC版のOFN_CREATEPROMPTと同じく、無いファイル名なら「作成しますか?」を聞くダイアログで選ばせる
        private void BrowseFile(TextBox textBox)
        {
            String path = ShowOpenFileDialog(this);
            if (path != null)
            {
                textBox.Text = path;
            }
        }

        private void EnableControls(Boolean running)
        {
            button_Split.IsEnabled = !running;
            button_Merge.IsEnabled = !running;
            button_Stop.IsEnabled = running;
        }

        // ES_NUMBER相当: 数字以外は入力させない
        private void textBox_Size_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsDigits(e.Text);
        }

        // スペースはPreviewTextInputに来ないので、キーの段階で止める
        private void textBox_Size_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void textBox_Size_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            String text = e.SourceDataObject.GetData(DataFormats.UnicodeText) as String;
            if (text == null || !IsDigits(text))
            {
                e.CancelCommand();
            }
        }

        private static Boolean IsDigits(String text)
        {
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
