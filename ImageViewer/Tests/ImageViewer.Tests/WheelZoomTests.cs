using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageViewer.Tests
{
    /// <summary>
    /// WheelZoom（マウスホイールでの拡大/縮小の倍率計算。画面に依存しない）のテスト。
    /// </summary>
    [TestClass]
    public class WheelZoomTests
    {
        private const double _tolerance = 1e-9;

        [TestMethod]
        public void 奥へ1ノッチ回すと0_1倍拡大する()
        {
            var zoom = new WheelZoom();

            Assert.AreEqual(1.1, zoom.Apply(1.0, 120), _tolerance);
        }

        [TestMethod]
        public void 手前へ1ノッチ回すと0_1倍縮小する()
        {
            var zoom = new WheelZoom();

            Assert.AreEqual(0.9, zoom.Apply(1.0, -120), _tolerance);
        }

        [TestMethod]
        public void 一度に3ノッチ分回すと0_3倍変わる()
        {
            var zoom = new WheelZoom();

            Assert.AreEqual(1.3, zoom.Apply(1.0, 360), _tolerance);
            Assert.AreEqual(1.0, zoom.Apply(1.3, -360), _tolerance);
        }

        [TestMethod]
        public void 上限の5倍より大きくはならない()
        {
            var zoom = new WheelZoom();

            Assert.AreEqual(WheelZoom.MaxScale, zoom.Apply(5.0, 120), _tolerance);
            Assert.AreEqual(WheelZoom.MaxScale, zoom.Apply(4.9, 1200), _tolerance);
        }

        [TestMethod]
        public void 下限の0_1倍より小さくはならない()
        {
            var zoom = new WheelZoom();

            Assert.AreEqual(WheelZoom.MinScale, zoom.Apply(0.1, -120), _tolerance);
            Assert.AreEqual(WheelZoom.MinScale, zoom.Apply(0.3, -1200), _tolerance);
        }

        // スライダーの値は0.1を足し引きした誤差(0.30000000000000004等)を含むことがあるが、結果は0.1刻みにそろう
        [TestMethod]
        public void 浮動小数点の誤差を含む倍率からでも0_1刻みの値になる()
        {
            var zoom = new WheelZoom();

            Assert.AreEqual(0.4, zoom.Apply(0.1 + 0.1 + 0.1, 120));
        }

        // 高精度タッチパッドは1ノッチ(120)より小さなDeltaを細かく送ってくる
        [TestMethod]
        public void ノッチ未満の回転量は溜めて1ノッチ分たまったら1段階動く()
        {
            var zoom = new WheelZoom();

            Assert.AreEqual(1.0, zoom.Apply(1.0, 40), _tolerance);
            Assert.AreEqual(1.0, zoom.Apply(1.0, 40), _tolerance);
            Assert.AreEqual(1.1, zoom.Apply(1.0, 40), _tolerance);
            Assert.AreEqual(1.1, zoom.Apply(1.1, 40), _tolerance, "1ノッチ分使った残りは0から溜め直すはず");
        }

        [TestMethod]
        public void 回す向きが変わったら逆向きの溜めは捨てる()
        {
            var zoom = new WheelZoom();

            Assert.AreEqual(1.0, zoom.Apply(1.0, 60), _tolerance);
            Assert.AreEqual(0.9, zoom.Apply(1.0, -120), _tolerance, "+60の溜めと相殺されずに1段階縮小するはず");
        }

        [TestMethod]
        public void Normalizeは0_1刻みに丸めて範囲内に収める()
        {
            Assert.AreEqual(1.1, WheelZoom.Normalize(1.06), _tolerance);
            Assert.AreEqual(1.0, WheelZoom.Normalize(1.04), _tolerance);
            Assert.AreEqual(WheelZoom.MaxScale, WheelZoom.Normalize(10.0), _tolerance);
            Assert.AreEqual(WheelZoom.MinScale, WheelZoom.Normalize(0.0), _tolerance);
            Assert.AreEqual(WheelZoom.MinScale, WheelZoom.Normalize(-1.0), _tolerance);
        }

        [TestMethod]
        public void KeepPointUnderMouseは2倍に拡大するとマウスの下の点がずれない位置を返す()
        {
            // 内容上の位置150(=50+100)は2倍で300になるので、マウス位置100を引いた200までスクロールする
            Assert.AreEqual(200.0, WheelZoom.KeepPointUnderMouse(50.0, 100.0, 2.0), _tolerance);
        }

        [TestMethod]
        public void KeepPointUnderMouseは縮小するとスクロール位置を戻す()
        {
            // 内容上の位置250(=200+50)は0.8倍で200になり、マウス位置50を引いて150
            Assert.AreEqual(150.0, WheelZoom.KeepPointUnderMouse(200.0, 50.0, 0.8), _tolerance);
        }

        [TestMethod]
        public void KeepPointUnderMouseは負のスクロール位置を返さない()
        {
            Assert.AreEqual(0.0, WheelZoom.KeepPointUnderMouse(50.0, 100.0, 0.5), _tolerance);
        }
    }
}
