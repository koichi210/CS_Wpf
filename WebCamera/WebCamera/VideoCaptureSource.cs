using OpenCvSharp;

namespace WebCamera
{
    /// <summary>
    /// OpenCvSharp の VideoCapture でPCのカメラから映像を読む IFrameSource。
    /// </summary>
    internal sealed class VideoCaptureSource : IFrameSource
    {
        private readonly VideoCapture _capture;
        private readonly Mat _frame = new Mat();

        // 開くのに数秒かかることがあるので、UIスレッドではなくワーカースレッドで生成すること
        public VideoCaptureSource(int cameraIndex)
        {
            _capture = new VideoCapture(cameraIndex);
        }

        public bool IsOpened
        {
            get { return _capture.IsOpened(); }
        }

        public bool Read()
        {
            // カメラが抜かれた等で取得できなくなると、Read が false を返すか空の Mat になる
            return _capture.Read(_frame) && !_frame.Empty();
        }

        public CameraFrame CopyFrame()
        {
            return FrameConverter.ToFrame(_frame);
        }

        public void Dispose()
        {
            _frame.Dispose();
            _capture.Dispose();
        }
    }
}
