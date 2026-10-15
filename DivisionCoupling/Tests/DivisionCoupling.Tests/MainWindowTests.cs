using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        public void 参照で選んだファイル名が分割と結合の欄に入る()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();
                Window owner = null;
                window.ShowOpenFileDialog = w =>
                {
                    owner = w;
                    return @"C:\work\new.bin";
                };

                ClickButton(window, "button_SplitBrowse");
                Assert.AreSame(window, owner);
                Assert.AreEqual(@"C:\work\new.bin", ((TextBox)window.FindName("textBox_SplitFileName")).Text);

                ClickButton(window, "button_MergeBrowse");
                Assert.AreEqual(@"C:\work\new.bin", ((TextBox)window.FindName("textBox_MergeFileName")).Text);
            });
        }

        [TestMethod]
        public void 参照をキャンセルしたら欄はそのまま()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();
                TextBox split = (TextBox)window.FindName("textBox_SplitFileName");
                TextBox merge = (TextBox)window.FindName("textBox_MergeFileName");
                split.Text = "before_split";
                merge.Text = "before_merge";
                window.ShowOpenFileDialog = w => null;

                ClickButton(window, "button_SplitBrowse");
                ClickButton(window, "button_MergeBrowse");

                Assert.AreEqual("before_split", split.Text);
                Assert.AreEqual("before_merge", merge.Text);
            });
        }

        [TestMethod]
        public void 参照ボタンは作成するか聞くダイアログを使う()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow();
                Func<Window, String> expected = FileOpenDialog.Show;

                Assert.AreEqual(expected, window.ShowOpenFileDialog);
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

        private static void ClickButton(MainWindow window, String name)
        {
            ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }
    }
}
