using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace WebCamera
{
    /// <summary>
    /// Webカメラのライブ映像を表示し、静止画(PNG)を保存するツール(WinForms版WebCameraのWPF移植)。
    ///
    /// 【スレッドの役割分担】
    /// - カメラを開く・フレームを読む・画素をコピーする … CaptureWorker(ワーカースレッド)
    /// - image_Preview への表示(WriteableBitmap への書き込み) … UIスレッド(Dispatcher 経由で受け取る)
    /// ワーカーはフレーム毎に「独立した」CameraFrame を作ってUIへ渡し、以後そのフレームには触らない。
    ///
    /// 【閉じるとき】
    /// ワーカーが動いていたら、いったん閉じるのを取り消して停止を要求し、
    /// ワーカーがカメラを解放して終わったのを受けてから改めて Close() する(WinForms版と同じ)。
    /// </summary>
    public partial class MainWindow : Window
    {
        // 動いているワーカー。停止の完了をUIスレッドで受け取るまでは null にしない
        // (WinForms版の BackgroundWorker.IsBusy と同じく「止めている最中」も実行中として扱うため)
        private CaptureWorker _captureWorker;

        // ウィンドウを閉じようとしたときにワーカーが動いていたら、ワーカーの終了を待ってから改めて閉じる。そのための目印
        private bool _closeRequested;

        private readonly PreviewSurface _preview = new PreviewSurface();

        public MainWindow()
        {
            InitializeComponent();
            UpdateButtons();
        }

        // カメラの代わりに偽物の映像源を差し込めるよう、テストからだけ差し替える
        internal Func<IFrameSource> FrameSourceFactory { get; set; } = () => new VideoCaptureSource(0);

        // テスト用: ワーカーが動いているか(停止処理中を含む)
        internal bool IsCapturing
        {
            get { return _captureWorker != null; }
        }

        private void button_StartStop_Click(object sender, RoutedEventArgs e)
        {
            if (_captureWorker != null)
            {
                // 停止要求。カメラの解放はワーカー自身が行い、OnCaptureCompleted で停止状態に戻る
                _captureWorker.CancelAsync();
                button_StartStop.IsEnabled = false;
                return;
            }

            // 画像取得スレッド開始(カメラを開くのもワーカー側。開くのに数秒かかってもUIは固まらない)
            _captureWorker = new CaptureWorker(FrameSourceFactory, PostFrame, PostCompleted);
            _captureWorker.Start();
            UpdateButtons();
        }

        // ワーカースレッドから呼ばれる。フレームと完了通知は同じ優先度で積むので、
        // 完了通知より先に積んだフレームが後から届くことはない(WinForms版の ProgressChanged→RunWorkerCompleted の順と同じ)
        private void PostFrame(CaptureWorker worker, CameraFrame frame)
        {
            Dispatcher.BeginInvoke(new Action(() => OnFrameReady(worker, frame)));
        }

        private void PostCompleted(CaptureWorker worker, CaptureResult result)
        {
            Dispatcher.BeginInvoke(new Action(() => OnCaptureCompleted(worker, result)));
        }

        internal void OnFrameReady(CaptureWorker worker, CameraFrame frame)
        {
            worker.FrameConsumed();

            // 閉じている最中に届いたフレームは表示しない
            if (_closeRequested)
            {
                return;
            }

            // 描画
            if (_preview.Show(frame))
            {
                image_Preview.Source = _preview.Bitmap;
            }

            UpdateButtons();
        }

        internal void OnCaptureCompleted(CaptureWorker worker, CaptureResult result)
        {
            if (worker != _captureWorker)
            {
                return;
            }
            _captureWorker = null;

            if (_closeRequested)
            {
                // Window_Closing で保留していた終了を、ワーカーが止まった今あらためて実行する
                Close();
                return;
            }

            UpdateButtons();

            if (result.Error != null)
            {
                MessageBox.Show(this, "カメラの処理中にエラーが発生しました" + Environment.NewLine + result.Error.Message,
                    Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else if (!result.Cancelled && result.Message != null)
            {
                MessageBox.Show(this, result.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void button_Snapshot_Click(object sender, RoutedEventArgs e)
        {
            // ダイアログを開いている間も映像は更新され、表示中の Bitmap は上書きされていく。
            // 押した瞬間の映像を保存したいので、先に複製しておく
            BitmapSource snapshot = _preview.CreateSnapshot();
            if (snapshot == null)
            {
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                FileName = SnapshotFile.CreateDefaultFileName(DateTime.Now),
                InitialDirectory = SnapshotFile.GetInitialDirectory(),
                Filter = SnapshotFile.DialogFilter,
                DefaultExt = SnapshotFile.Extension,
                AddExtension = true,
                OverwritePrompt = true,
            };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                SnapshotFile.SavePng(snapshot, dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "画像を保存できませんでした" + Environment.NewLine + ex.Message,
                    Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 実行状態・映像の有無に合わせて、ボタンの表示と有効/無効を揃える。
        /// </summary>
        private void UpdateButtons()
        {
            bool running = _captureWorker != null;
            button_StartStop.Content = running ? "Stop" : "Start";
            button_StartStop.IsEnabled = !running || !_captureWorker.CancellationPending;
            // 停止後も最後に表示したフレームは残るので、それも保存できるようにしておく
            button_Snapshot.IsEnabled = _preview.Bitmap != null;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_captureWorker == null)
            {
                return;
            }

            // 待ちながらUIを回す代わりに、いったん閉じるのを取り消してワーカーに停止を要求する。
            // ワーカーがカメラを解放して終わると OnCaptureCompleted から Close() し直す
            e.Cancel = true;
            _closeRequested = true;
            _captureWorker.CancelAsync();
            button_StartStop.IsEnabled = false;
            button_Snapshot.IsEnabled = false;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // 最後に表示していた映像を手放す
            image_Preview.Source = null;
            _preview.Clear();
        }
    }
}
