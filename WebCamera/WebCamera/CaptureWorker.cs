using System;
using System.Threading;

namespace WebCamera
{
    /// <summary>
    /// カメラを開き、フレームを読み続けるワーカー(WinForms版の BackgroundWorker の DoWork に相当)。
    ///
    /// 【カメラの寿命】
    /// IFrameSource(カメラ)はワーカースレッドの中だけで生成・使用・破棄する(using)。
    /// 停止もウィンドウを閉じるときも、CancelAsync で止まってもらうだけで、カメラは必ずワーカー自身が解放する。
    /// UIスレッドとの取り合いが起きないよう、UI側からカメラには一切触らない。
    ///
    /// 【未表示フレームの間引き】
    /// UIの描画がカメラのフレームレートに追いつかないとき、未処理のフレームが Dispatcher のキューに
    /// 溜まり続けてメモリを食い潰さないよう、UIがまだ表示していないフレームが MaxPendingFrames 枚あれば
    /// 読んだフレームを捨てる。UIは表示し終えたら FrameConsumed を呼んで知らせる。
    ///
    /// コールバック(frameReady/completed)はワーカースレッドから呼ばれる。UIへの受け渡しは呼び出し側で Dispatcher を使う。
    /// </summary>
    internal sealed class CaptureWorker
    {
        internal const int MaxPendingFrames = 2;

        private readonly Func<IFrameSource> _openSource;
        private readonly Action<CaptureWorker, CameraFrame> _frameReady;
        private readonly Action<CaptureWorker, CaptureResult> _completed;
        private Thread _thread;
        private volatile bool _cancellationPending;

        // UIがまだ表示していない(FrameConsumed が呼ばれていない)フレーム数
        private int _pendingFrames;

        public CaptureWorker(Func<IFrameSource> openSource,
            Action<CaptureWorker, CameraFrame> frameReady, Action<CaptureWorker, CaptureResult> completed)
        {
            if (openSource == null)
            {
                throw new ArgumentNullException(nameof(openSource));
            }
            if (frameReady == null)
            {
                throw new ArgumentNullException(nameof(frameReady));
            }
            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }
            _openSource = openSource;
            _frameReady = frameReady;
            _completed = completed;
        }

        public bool CancellationPending
        {
            get { return _cancellationPending; }
        }

        internal int PendingFrames
        {
            get { return Volatile.Read(ref _pendingFrames); }
        }

        public void Start()
        {
            if (_thread != null)
            {
                throw new InvalidOperationException("ワーカーは1回しか開始できない");
            }
            // カメラを開くのもワーカー側。開くのに数秒かかってもUIは固まらない。
            // IsBackground にしておくのは、万一カメラのドライバーが Read から戻らなくてもプロセスを終了できるようにするため
            _thread = new Thread(Run) { IsBackground = true, Name = "WebCamera.Capture" };
            _thread.Start();
        }

        // 停止要求。実際に止まったら completed が呼ばれる
        public void CancelAsync()
        {
            _cancellationPending = true;
        }

        // UIがフレームを1枚表示し終えた(または捨てた)ことを知らせる
        public void FrameConsumed()
        {
            Interlocked.Decrement(ref _pendingFrames);
        }

        // テスト用: ワーカースレッドの終了を待つ
        internal bool Join(TimeSpan timeout)
        {
            return _thread == null || _thread.Join(timeout);
        }

        private void Run()
        {
            CaptureResult result;
            try
            {
                result = Capture();
            }
            catch (Exception ex)
            {
                result = CaptureResult.Failed(ex);
            }
            _completed(this, result);
        }

        private CaptureResult Capture()
        {
            using (IFrameSource source = _openSource())
            {
                if (!source.IsOpened)
                {
                    return CaptureResult.Stopped("カメラが見つかりませんでした");
                }

                while (!_cancellationPending)
                {
                    if (!source.Read())
                    {
                        // カメラが抜かれた等で取得できなくなったら、空回りせずに終了する
                        return CaptureResult.Stopped("カメラから画像を取得できなくなりました");
                    }

                    // UIが前のフレームをまだ表示できていなければ、このフレームは捨てる
                    // (読むのは止めない。止めるとカメラ側のバッファに古いフレームが残り、表示が遅れるため)
                    if (Volatile.Read(ref _pendingFrames) >= MaxPendingFrames)
                    {
                        continue;
                    }

                    CameraFrame frame = source.CopyFrame();
                    Interlocked.Increment(ref _pendingFrames);
                    _frameReady(this, frame);
                }

                return CaptureResult.Canceled;
            }
        }
    }

    /// <summary>
    /// ワーカーが止まった理由(WinForms版の RunWorkerCompletedEventArgs の Cancelled/Result/Error に相当)。
    /// </summary>
    internal sealed class CaptureResult
    {
        public static readonly CaptureResult Canceled = new CaptureResult(true, null, null);

        private CaptureResult(bool cancelled, string message, Exception error)
        {
            Cancelled = cancelled;
            Message = message;
            Error = error;
        }

        // 停止要求で止まった
        public bool Cancelled { get; }

        // カメラが無い・読めなくなった等、利用者に知らせる理由
        public string Message { get; }

        // 想定外の例外で止まった
        public Exception Error { get; }

        public static CaptureResult Stopped(string message)
        {
            return new CaptureResult(false, message, null);
        }

        public static CaptureResult Failed(Exception error)
        {
            return new CaptureResult(false, null, error);
        }
    }
}
