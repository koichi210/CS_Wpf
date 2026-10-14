using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StandardTemplate.Wpf.Tests;

namespace MotionCapture.Tests
{
    /// <summary>
    /// MainWindow / SampleCaptAreaWindow を実際に生成して、MFC版のダイアログと同じ構成になっているかを確認するテスト。
    /// 表示はせず、作って中身を見るだけにする（録画は走らせない）。
    /// </summary>
    [TestClass]
    public class MainWindowTests
    {
        [TestMethod]
        public void 起動直後はMFC版と同じ初期値が入っている()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();

                Assert.AreEqual("MotionCapture", window.Title);
                Assert.AreEqual(@"c:\ScreenCapture.avi", Text(window, "textBox_SaveFileName"));
                Assert.AreEqual("0", Text(window, "textBox_CaptRectX"));
                Assert.AreEqual("0", Text(window, "textBox_CaptRectY"));
                Assert.AreEqual("1920", Text(window, "textBox_CaptRectWidth"));
                Assert.AreEqual("1080", Text(window, "textBox_CaptRectHeight"));
                Assert.AreEqual("24", Text(window, "textBox_CaptBpp"));
                Assert.AreEqual("20", Text(window, "textBox_CaptFps"));
                Assert.AreEqual("0", Text(window, "textBox_SkipFrame"));
                Assert.AreEqual("10", Text(window, "textBox_TimeoutSec"));
                Assert.AreEqual("1024", Text(window, "textBox_ResizeRectWidth"));
                Assert.AreEqual("768", Text(window, "textBox_ResizeRectHeight"));
                Assert.AreEqual(false, ((CheckBox)window.FindName("checkBox_Resize")).IsChecked);
                Assert.AreEqual(true, ((CheckBox)window.FindName("checkBox_MousePointRec")).IsChecked);
            });
        }

        [TestMethod]
        public void ボタンとチェックボックスの文言がMFC版と同じ()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();

                Assert.AreEqual("記録領域の確認", ((Button)window.FindName("button_CaptAreaSample")).Content);
                Assert.AreEqual("記録開始", ((Button)window.FindName("button_Record")).Content);
                Assert.AreEqual("記録停止", ((Button)window.FindName("button_RecordStop")).Content);
                Assert.AreEqual("リサイズ有効", ((CheckBox)window.FindName("checkBox_Resize")).Content);
                Assert.AreEqual("マウスポインタを記録", ((CheckBox)window.FindName("checkBox_MousePointRec")).Content);
                Assert.AreEqual(ResizeMode.CanMinimize, window.ResizeMode);
            });
        }

        [TestMethod]
        public void 記録領域の確認ダイアログは領域をタイトルに出し縮小画像を表示する()
        {
            StaRunner.Run(() =>
            {
                SampleCaptAreaWindow window = new SampleCaptAreaWindow(0, 0, 1920, 1080);

                Assert.AreEqual("キャプチャ領域 (0, 0) 1920 x 1080", window.Title);
                BitmapSource image = (BitmapSource)((Image)window.FindName("image_Sample")).Source;
                Assert.AreEqual(700, image.PixelWidth);
                Assert.AreEqual(393, image.PixelHeight);
                Button close = (Button)window.FindName("button_Close");
                Assert.AreEqual("閉じる", close.Content);
                Assert.IsTrue(close.IsDefault);
                Assert.IsTrue(close.IsCancel);
            });
        }

        private static String Text(Window window, String name)
        {
            return ((TextBox)window.FindName(name)).Text;
        }
    }
}
