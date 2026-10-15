using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MotionCapture
{
    /// <summary>
    /// MFC版MotionCaptureDlg（画面の指定領域をAVIに録画する）のWPF版。
    /// 入力欄の読み取りとチェックはMFCのUpdateData(TRUE)と同じ順番・同じメッセージで行う(Logic.Exchange)。
    /// </summary>
    public partial class MainWindow : Window
    {
        // MFCのAfxMessageBoxは、キャプションにアプリ名(exe名)を出す
        private const String _appName = "MotionCapture";

        private readonly ManageAvi _avi = new ManageAvi();
        private readonly CaptureSettings _settings = new CaptureSettings();
        private Boolean _closing = false;   // 終了処理中（録画の結果を表示しない）
        private Boolean _confirming = false;    // 録画中の確認を出している（その間に届いた録画の結果は預かる）
        private String _pendingText = null;     // 確認の間に届いて預かっている録画の結果（無ければnull）
        private String _pendingCaption = null;

        /// <summary>
        /// メッセージボックス(本文, キャプション, ボタン)。テストでは差し替えて、出したメッセージと答えを確かめる。
        /// </summary>
        internal Func<String, String, MessageBoxButton, MessageBoxResult> ShowMessage { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            ShowMessage = (text, caption, button) => MessageBox.Show(this, text, caption, button);

            // MFCのOnInitDialog → UpdateData(FALSE) と同じく、初期値を入力欄に出す
            UpdateControls();

            // 録画の結果はUIスレッドで出す。録画スレッドはファイルを閉じたら投げるだけですぐ終わるので、閉じるときに待てる
            _avi.Notify = (text, caption) => Dispatcher.BeginInvoke(new Action(() => ShowRecordResult(text, caption)));

            // シャットダウン・ログオフでは、この後すぐプロセスごと終了させられるので、確認せずに止めてファイルを閉じさせる
            if (Application.Current != null)
            {
                Application.Current.SessionEnding += (s, e) => StopRecordAndWait();
            }
        }

        // ダイアログと同じく、最初の入力欄にフォーカスを置いて全選択しておく
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            textBox_SaveFileName.Focus();
            textBox_SaveFileName.SelectAll();
        }

        // Enter キーでは閉じない（MFC版のOnBnClickedOkは何もしない）。Escはダイアログと同じく閉じる
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void button_Record_Click(object sender, RoutedEventArgs e)
        {
            if (_avi.IsExecution())
            {
                if (ConfirmWhileRecording("実行中です。中断しますか？"))
                {
                    button_RecordStop_Click(sender, e);
                }
                // 確認している間に録画が終わっていたら、その結果を確認の後に出す
                ShowPendingResult();
                return;
            }

            if (!UpdateData())
            {
                return;
            }

            // 別プロセスが保存先を使っていないか、実際に開いて確かめる
            if (!CanOpenSaveFile(_settings.SaveFileName))
            {
                ShowMessage("別プロセスが使用中です。", "Warning", MessageBoxButton.OK);
                return;
            }

            _avi.SetSaveFileName(_settings.SaveFileName);
            _avi.SetFrameRate(unchecked((uint)_settings.FrameRate));
            _avi.SetRecordSec(_settings.TimeoutSec);
            _avi.SetSkipFrame(_settings.SkipFrame);
            _avi.SetCaptureRect(_settings.CaptRectX, _settings.CaptRectY, _settings.CaptRectWidth, _settings.CaptRectHeight);
            _avi.SetCaptureQuality(_settings.BitmapBpp);
            _avi.SetResize(_settings.Resize, _settings.ResizeRectWidth, _settings.ResizeRectHeight);
            _avi.SetRecordMousePoint(_settings.MousePointRec);

            _avi.StartRecord();
        }

        private void button_RecordStop_Click(object sender, RoutedEventArgs e)
        {
            _avi.StopRecord();
        }

        // 録画中のまま閉じると録画スレッドごと打ち切られてAVIファイルが壊れるので、止めてファイルを閉じ終わってから閉じる
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!StopRecordForClose())
            {
                e.Cancel = true;
            }
        }

        // 録画中なら確認のうえ中断し、ファイルを閉じ終わるまで待つ（閉じてよければ true）
        internal Boolean StopRecordForClose()
        {
            // 停止後にファイルを閉じている途中なら、確認はせずに待つだけにする
            if (_avi.IsRecording())
            {
                if (!ConfirmWhileRecording("実行中です。中断して閉じますか？"))
                {
                    // 閉じないので、確認している間に届いた結果（タイムアウトなど）はここで出す
                    ShowPendingResult();
                    return false;
                }
                // 閉じるので、確認している間に届いた結果は出さない（録画がもう終わっていれば、下の待ちはすぐ終わる）
                _pendingText = null;
            }

            StopRecordAndWait();
            return true;
        }

        // 録画中の確認（はい/いいえ）。答えるまでの間に録画が終わっても、結果のメッセージを確認の上に重ねて出さないよう
        // ShowRecordResultに預からせる。預かった結果は、呼び出し側が答えに応じてShowPendingResultで出すか捨てる
        private Boolean ConfirmWhileRecording(String text)
        {
            _confirming = true;
            try
            {
                return ShowMessage(text, "Warning", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
            }
            finally
            {
                _confirming = false;
            }
        }

        private void ShowPendingResult()
        {
            String text = _pendingText;
            if (text == null)
            {
                return;
            }
            _pendingText = null;
            ShowRecordResult(text, _pendingCaption);
        }

        // 確認せずに中断し、ファイルを閉じ終わるまで待つ
        private void StopRecordAndWait()
        {
            _closing = true;
            IsEnabled = false;  // 待っている間に操作させない
            _avi.StopRecordAndWait();
            IsEnabled = true;
        }

        // 録画スレッドからの結果（UIスレッドで呼ばれる）。ファイルはもう閉じてある
        private void ShowRecordResult(String text, String caption)
        {
            // 閉じる途中は終了を妨げないよう、何も表示しない（「保存しました」も、閉じると答えた後には出さない）
            if (_closing)
            {
                return;
            }

            // 確認を出している最中なら、答えが出るまで預かる
            if (_confirming)
            {
                _pendingText = text;
                _pendingCaption = caption;
                return;
            }
            ShowMessage(text, caption, MessageBoxButton.OK);
        }

        // テスト用: 画面を通さずに録画の設定・開始をする
        internal ManageAvi Avi
        {
            get { return _avi; }
        }

        private void button_CaptAreaSample_Click(object sender, RoutedEventArgs e)
        {
            if (!UpdateData())
            {
                return;
            }

            SampleCaptAreaWindow window = new SampleCaptAreaWindow(
                _settings.CaptRectX, _settings.CaptRectY, _settings.CaptRectWidth, _settings.CaptRectHeight);
            window.Owner = this;
            window.ShowDialog();
        }

        // MFCのUpdateData(TRUE)相当。失敗したらメッセージを出し、その入力欄にフォーカスして全選択する
        private Boolean UpdateData()
        {
            DialogInput input = new DialogInput
            {
                SaveFileName = textBox_SaveFileName.Text,
                CaptRectX = textBox_CaptRectX.Text,
                CaptRectY = textBox_CaptRectY.Text,
                CaptRectWidth = textBox_CaptRectWidth.Text,
                CaptRectHeight = textBox_CaptRectHeight.Text,
                CaptBpp = textBox_CaptBpp.Text,
                CaptFps = textBox_CaptFps.Text,
                TimeoutSec = textBox_TimeoutSec.Text,
                SkipFrame = textBox_SkipFrame.Text,
                ResizeRectWidth = textBox_ResizeRectWidth.Text,
                ResizeRectHeight = textBox_ResizeRectHeight.Text,
                Resize = checkBox_Resize.IsChecked == true,
                MousePointRec = checkBox_MousePointRec.IsChecked == true,
            };

            InputField failedField;
            String message;
            if (Logic.Exchange(input, _settings, out failedField, out message))
            {
                return true;
            }

            MessageBox.Show(this, message, _appName, MessageBoxButton.OK, MessageBoxImage.Exclamation);
            TextBox failedBox = GetTextBox(failedField);
            if (failedBox != null)
            {
                failedBox.Focus();
                failedBox.SelectAll();
            }
            return false;
        }

        // MFCのUpdateData(FALSE)相当
        private void UpdateControls()
        {
            textBox_SaveFileName.Text = _settings.SaveFileName;
            textBox_CaptRectX.Text = Logic.Format(_settings.CaptRectX);
            textBox_CaptRectY.Text = Logic.Format(_settings.CaptRectY);
            textBox_CaptRectWidth.Text = Logic.Format(_settings.CaptRectWidth);
            textBox_CaptRectHeight.Text = Logic.Format(_settings.CaptRectHeight);
            textBox_CaptBpp.Text = Logic.Format(_settings.BitmapBpp);
            textBox_CaptFps.Text = Logic.Format(_settings.FrameRate);
            textBox_TimeoutSec.Text = Logic.Format(_settings.TimeoutSec);
            textBox_SkipFrame.Text = Logic.Format(_settings.SkipFrame);
            textBox_ResizeRectWidth.Text = Logic.Format(_settings.ResizeRectWidth);
            textBox_ResizeRectHeight.Text = Logic.Format(_settings.ResizeRectHeight);
            checkBox_Resize.IsChecked = _settings.Resize;
            checkBox_MousePointRec.IsChecked = _settings.MousePointRec;
        }

        private TextBox GetTextBox(InputField field)
        {
            switch (field)
            {
                case InputField.SaveFileName: return textBox_SaveFileName;
                case InputField.CaptRectX: return textBox_CaptRectX;
                case InputField.CaptRectY: return textBox_CaptRectY;
                case InputField.CaptRectWidth: return textBox_CaptRectWidth;
                case InputField.CaptRectHeight: return textBox_CaptRectHeight;
                case InputField.CaptBpp: return textBox_CaptBpp;
                case InputField.CaptFps: return textBox_CaptFps;
                case InputField.TimeoutSec: return textBox_TimeoutSec;
                case InputField.SkipFrame: return textBox_SkipFrame;
                case InputField.ResizeRectWidth: return textBox_ResizeRectWidth;
                case InputField.ResizeRectHeight: return textBox_ResizeRectHeight;
                default: return null;
            }
        }

        /// <summary>
        /// 別プロセスが保存先を使っていないか、共有なしで実際に開いて確かめる。
        /// 既存のファイルは切り詰めない（以前はMFC版のCFile::Open(modeCreate)と同じく空にしていたので、
        /// 圧縮の選択をキャンセルすると元の中身が消えていた）。無ければ作って確かめ、閉じると同時に消す（0バイトのファイルを残さない）。
        /// </summary>
        internal static Boolean CanOpenSaveFile(String path)
        {
            try
            {
                try
                {
                    using (new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                    }
                }
                catch (FileNotFoundException)
                {
                    using (new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
                    {
                    }
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException
                || ex is ArgumentException || ex is NotSupportedException || ex is System.Security.SecurityException)
            {
                return false;
            }
        }
    }
}
