using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StandardTemplate.Wpf.Tests;

namespace DivisionCoupling.Tests
{
    /// <summary>
    /// MainWindowを実際に生成して、MFC版のダイアログと同じ構成になっているかを確認するテスト。
    /// 表示はせず、作って中身を見るだけにする（分割・結合は走らせない）。
    /// </summary>
    [TestClass]
    public class MainWindowTests
    {
        [TestMethod]
        public void タイトルとボタンの文言がMFC版と同じ()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();

                Assert.AreEqual("ファイル分割", window.Title);
                Assert.AreEqual("分割", ((Button)window.FindName("button_Split")).Content);
                Assert.AreEqual("結合", ((Button)window.FindName("button_Merge")).Content);
                Assert.AreEqual("参照", ((Button)window.FindName("button_SplitBrowse")).Content);
                Assert.AreEqual("参照", ((Button)window.FindName("button_MergeBrowse")).Content);
                Assert.AreEqual("停止", ((Button)window.FindName("button_Stop")).Content);
                Assert.AreEqual("閉じる", ((Button)window.FindName("button_Close")).Content);
            });
        }

        [TestMethod]
        public void 起動直後は停止ボタンだけ押せない()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();

                Assert.IsTrue(((Button)window.FindName("button_Split")).IsEnabled);
                Assert.IsTrue(((Button)window.FindName("button_Merge")).IsEnabled);
                Assert.IsFalse(((Button)window.FindName("button_Stop")).IsEnabled);
            });
        }

        [TestMethod]
        public void 閉じるボタンはEnterとEscの両方で押される()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();
                Button close = (Button)window.FindName("button_Close");

                Assert.IsTrue(close.IsDefault);
                Assert.IsTrue(close.IsCancel);
            });
        }

        [TestMethod]
        public void 大きさは変えられず最小化だけできる()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();

                Assert.AreEqual(ResizeMode.CanMinimize, window.ResizeMode);
                Assert.AreEqual(SizeToContent.WidthAndHeight, window.SizeToContent);
            });
        }
    }
}
