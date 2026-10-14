using System;
using System.Threading;
using System.Windows.Media;

namespace WebCamera.Tests
{
    /// <summary>
    /// カメラの代わりにワーカーへ渡す偽物の映像源。
    /// どのスレッドで生成・破棄されたか、何回読まれたかを記録する。
    /// </summary>
    internal sealed class FakeFrameSource : IFrameSource
    {
        private int _readCount;

        public FakeFrameSource(bool isOpened = true, int failAfterReads = -1, Exception throwOnRead = null)
        {
            IsOpened = isOpened;
            FailAfterReads = failAfterReads;
            ThrowOnRead = throwOnRead;
            CreatedThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public bool IsOpened { get; }

        // この回数だけ読めたら、以後は読めなくなる(カメラが抜かれた想定)。負なら無制限
        public int FailAfterReads { get; }

        public Exception ThrowOnRead { get; }

        public int CreatedThreadId { get; }

        public int DisposedThreadId { get; private set; }

        public bool IsDisposed { get; private set; }

        public int ReadCount
        {
            get { return Volatile.Read(ref _readCount); }
        }

        public bool Read()
        {
            if (ThrowOnRead != null)
            {
                throw ThrowOnRead;
            }
            if (FailAfterReads >= 0 && ReadCount >= FailAfterReads)
            {
                return false;
            }
            Interlocked.Increment(ref _readCount);
            // 本物のカメラの Read はフレーム間隔だけ待つ。空回りでCPUを使い切らないよう少し待つ
            Thread.Sleep(1);
            return true;
        }

        public CameraFrame CopyFrame()
        {
            // 2x1 の BGR 画像。1画素目の青に何回目の読み込みかを入れておく
            byte[] pixels = { (byte)ReadCount, 0, 0, 0, 0, 255 };
            return new CameraFrame(2, 1, 6, PixelFormats.Bgr24, pixels);
        }

        public void Dispose()
        {
            IsDisposed = true;
            DisposedThreadId = Thread.CurrentThread.ManagedThreadId;
        }
    }
}
