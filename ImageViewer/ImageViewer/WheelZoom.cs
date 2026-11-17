using System;

namespace ImageViewer
{
    // マウスホイールによる拡大/縮小の倍率計算。画面に依存しない計算だけを切り出してあるのでテストできる
    // (倍率の範囲と刻みはMainWindow.xamlのSliderScaleのMinimum/Maximum/SmallChangeと揃えてある)
    public sealed class WheelZoom
    {
        public const double MinScale = 0.1;
        public const double MaxScale = 5.0;
        public const double Step = 0.1;

        // ホイール1ノッチ分の回転量(WPFのMouseWheelEventArgs.Deltaの単位)
        private const int _deltaPerNotch = 120;

        // 1ノッチに満たない回転量の溜め。高精度タッチパッドなどは小さなDeltaを細かく送ってくるので、
        // 1ノッチ分たまった時点で1段階だけ動かす
        private int _pendingDelta;

        // 現在の倍率とホイールの回転量から、次の倍率を返す(奥へ回すと拡大、手前へ回すと縮小)
        public double Apply(double currentScale, int wheelDelta)
        {
            // 回す向きが変わったら、逆向きの溜めは捨てる(戻したのに反応が遅れるのを防ぐ)
            if ((wheelDelta > 0 && _pendingDelta < 0) || (wheelDelta < 0 && _pendingDelta > 0))
            {
                _pendingDelta = 0;
            }

            _pendingDelta += wheelDelta;
            int notches = _pendingDelta / _deltaPerNotch;
            _pendingDelta -= notches * _deltaPerNotch;

            return Normalize(currentScale + notches * Step);
        }

        // 0.1刻みに丸めて、上限・下限の中に収める(0.1を足し引きした時の浮動小数点の誤差も消える)
        public static double Normalize(double scale)
        {
            double rounded = Math.Round(scale, 1, MidpointRounding.AwayFromZero);
            return Math.Min(MaxScale, Math.Max(MinScale, rounded));
        }

        // マウスの下にあった点が、拡大/縮小の後もマウスの下に残るようにするためのスクロール位置を返す。
        // offset: 拡大/縮小前のスクロール位置、mousePosition: 表示領域の左上から見たマウスの位置、
        // ratio: 新しい倍率÷元の倍率。内容は倍率に比例して大きさが変わるので、
        // 内容上の位置(offset + mousePosition)をratio倍した所がマウスの下に来るようにずらす
        public static double KeepPointUnderMouse(double offset, double mousePosition, double ratio)
        {
            return Math.Max(0.0, (offset + mousePosition) * ratio - mousePosition);
        }
    }
}
