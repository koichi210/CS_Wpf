using System;
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

        public MainWindow()
        {
            InitializeComponent();

            // MFCのOnInitDialog → UpdateData(FALSE) と同じく、初期値を入力欄に出す
            UpdateControls();
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
                if (MessageBox.Show(this, "実行中です。中断しますか？", "Warning", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    button_RecordStop_Click(sender, e);
                }
                return;
            }

            if (!UpdateData())
            {
                return;
            }

            // 別プロセスが保存先を使っていないか、実際に開いて確かめる（MFC版のCFile::Openと同じく排他で作り直す）
            if (!CanCreateFile(_settings.SaveFileName))
            {
                MessageBox.Show(this, "別プロセスが使用中です。", "Warning", MessageBoxButton.OK);
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

        // CFile::Open(modeCreate | modeWrite)と同じく、共有なしで作り直せるか（既存の中身は空になる）
        internal static Boolean CanCreateFile(String path)
        {
            try
            {
                using (new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
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
