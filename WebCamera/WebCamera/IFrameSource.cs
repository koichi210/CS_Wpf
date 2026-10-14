using System;

namespace WebCamera
{
    /// <summary>
    /// CaptureWorker がフレームを読む相手。本番は VideoCaptureSource(カメラ)。
    /// カメラの無い環境でもワーカーのスレッド処理をテストできるよう、インターフェースにしてある。
    /// 生成・使用・破棄はすべてワーカースレッド上で行われる。
    /// </summary>
    internal interface IFrameSource : IDisposable
    {
        bool IsOpened { get; }

        /// <summary>次のフレームを読み込む。取得できなかったら false。</summary>
        bool Read();

        /// <summary>直前に Read したフレームを、独立した CameraFrame にコピーして返す。</summary>
        CameraFrame CopyFrame();
    }
}
