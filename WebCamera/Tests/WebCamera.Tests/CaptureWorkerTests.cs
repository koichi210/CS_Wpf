using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WebCamera.Tests
{
    /// <summary>
    /// CaptureWorker(カメラを読み続けるワーカー)のスレッド処理のテスト。
    /// 本物のカメラ(VideoCapture)は使わず、FakeFrameSource を差し込んで検証する。
    /// </summary>
    [TestClass]
    public class CaptureWorkerTests
    {
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

        private FakeFrameSource _source;
        private ConcurrentQueue<CameraFrame> _frames;
        private ManualResetEventSlim _completedEvent;
        private CaptureResult _result;
        private int _completedThreadId;

        [TestInitialize]
        public void Setup()
        {
            _frames = new ConcurrentQueue<CameraFrame>();
            _completedEvent = new ManualResetEventSlim(false);
            _result = null;
        }

        [TestCleanup]
        public void Cleanup()
        {
            _completedEvent.Dispose();
        }

        private CaptureWorker CreateWorker(Func<FakeFrameSource> createSource)
        {
            return new CaptureWorker(
                () => _source = createSource(),
                (worker, frame) => _frames.Enqueue(frame),
                (worker, result) =>
                {
                    _result = result;
                    _completedThreadId = Thread.CurrentThread.ManagedThreadId;
                    _completedEvent.Set();
                });
        }

        private void WaitCompleted()
        {
            Assert.IsTrue(_completedEvent.Wait(_timeout), "ワーカーが止まらない");
        }

        private static void WaitUntil(Func<bool> condition, string message)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (!condition())
            {
                if (watch.Elapsed > _timeout)
                {
                    Assert.Fail(message);
                }
                Thread.Sleep(5);
            }
        }

        [TestMethod]
        public void カメラが開けなければ理由を返して止まりカメラは解放される()
        {
            CaptureWorker worker = CreateWorker(() => new FakeFrameSource(isOpened: false));

            worker.Start();
            WaitCompleted();

            Assert.IsFalse(_result.Cancelled);
            Assert.AreEqual("カメラが見つかりませんでした", _result.Message);
            Assert.IsNull(_result.Error);
            Assert.IsTrue(_source.IsDisposed);
            Assert.AreEqual(0, _frames.Count);
        }

        [TestMethod]
        public void 途中で読めなくなったら理由を返して止まる()
        {
            CaptureWorker worker = CreateWorker(() => new FakeFrameSource(failAfterReads: 1));

            worker.Start();
            WaitCompleted();

            Assert.IsFalse(_result.Cancelled);
            Assert.AreEqual("カメラから画像を取得できなくなりました", _result.Message);
            Assert.IsTrue(_source.IsDisposed);
            Assert.AreEqual(1, _frames.Count, "読めた1枚はUIへ渡している");
        }

        [TestMethod]
        public void 停止要求で止まりCancelledになる()
        {
            CaptureWorker worker = CreateWorker(() => new FakeFrameSource());

            worker.Start();
            WaitUntil(() => _frames.Count > 0, "フレームが届かない");
            Assert.IsFalse(worker.CancellationPending);
            worker.CancelAsync();
            Assert.IsTrue(worker.CancellationPending);
            WaitCompleted();

            Assert.IsTrue(_result.Cancelled);
            Assert.IsNull(_result.Message);
            Assert.IsNull(_result.Error);
            Assert.IsTrue(_source.IsDisposed);
        }

        [TestMethod]
        public void カメラの生成と破棄と完了通知はワーカースレッドで行う()
        {
            int testThreadId = Thread.CurrentThread.ManagedThreadId;
            CaptureWorker worker = CreateWorker(() => new FakeFrameSource());

            worker.Start();
            WaitUntil(() => _frames.Count > 0, "フレームが届かない");
            worker.CancelAsync();
            WaitCompleted();
            Assert.IsTrue(worker.Join(_timeout));

            Assert.AreNotEqual(testThreadId, _source.CreatedThreadId);
            Assert.AreEqual(_source.CreatedThreadId, _source.DisposedThreadId);
            Assert.AreEqual(_source.CreatedThreadId, _completedThreadId);
        }

        [TestMethod]
        public void 想定外の例外はErrorとして返しカメラは解放される()
        {
            InvalidOperationException error = new InvalidOperationException("テスト用の例外");
            CaptureWorker worker = CreateWorker(() => new FakeFrameSource(throwOnRead: error));

            worker.Start();
            WaitCompleted();

            Assert.AreSame(error, _result.Error);
            Assert.IsFalse(_result.Cancelled);
            Assert.IsTrue(_source.IsDisposed);
        }

        [TestMethod]
        public void UIが表示していないフレームが溜まったら間引く()
        {
            CaptureWorker worker = CreateWorker(() => new FakeFrameSource());

            worker.Start();
            // UIが1枚も表示しない(FrameConsumed を呼ばない)まま、カメラは読み続けている
            WaitUntil(() => _source != null && _source.ReadCount > 30, "カメラを読み続けていない");

            Assert.AreEqual(CaptureWorker.MaxPendingFrames, _frames.Count, "未表示が上限に達したら以後のフレームは捨てる");
            Assert.AreEqual(CaptureWorker.MaxPendingFrames, worker.PendingFrames);

            // 1枚表示し終えたら、また1枚だけ届く
            worker.FrameConsumed();
            WaitUntil(() => _frames.Count == CaptureWorker.MaxPendingFrames + 1, "表示後に次のフレームが届かない");
            int readCount = _source.ReadCount;
            WaitUntil(() => _source.ReadCount > readCount + 10, "カメラを読み続けていない");
            Assert.AreEqual(CaptureWorker.MaxPendingFrames + 1, _frames.Count);

            worker.CancelAsync();
            WaitCompleted();
        }

        [TestMethod]
        public void ワーカーは2回開始できない()
        {
            CaptureWorker worker = CreateWorker(() => new FakeFrameSource(isOpened: false));
            worker.Start();
            try
            {
                Assert.ThrowsException<InvalidOperationException>(() => worker.Start());
            }
            finally
            {
                WaitCompleted();
            }
        }
    }
}
