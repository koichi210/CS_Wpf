using System.Windows;
using System.Windows.Controls.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StandardTemplate.Wpf.Tests;

namespace Zodiac.Tests
{
    /// <summary>
    /// MainWindowを実際に生成して、ラジオボタン・年の選択・ボタン操作でMFC版と同じように表示が変わるかを確認するテスト。
    /// 「今年」は2026年に固定し、メッセージボックスは差し替えて中身だけ確認する。保存する設定は無いが、念のため Close() はしない。
    /// </summary>
    [TestClass]
    public class MainWindowTests
    {
        private sealed class ShownMessage
        {
            public string Text;
            public string Caption;
            public MessageBoxImage Icon;
            public int Count;
        }

        private static ShownMessage CaptureMessage(MainWindow window)
        {
            ShownMessage shown = new ShownMessage();
            window.ShowMessage = (text, caption, icon) =>
            {
                shown.Text = text;
                shown.Caption = caption;
                shown.Icon = icon;
                shown.Count++;
                return MessageBoxResult.OK;
            };
            return shown;
        }

        private static void Click(ButtonBase button)
        {
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }

        [TestMethod]
        public void 起動直後は今年と年齢が選ばれ満20才になっている()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow(2026);

                Assert.AreEqual("干支閲覧", window.Title);
                Assert.AreEqual(201, window.comboBoxYear.Items.Count);
                Assert.AreEqual(2026, window.comboBoxYear.SelectedItem);
                Assert.AreEqual(true, window.radioAge.IsChecked);
                Assert.AreEqual(ViewMode.Age, window.Mode);
                Assert.AreEqual(131, window.comboBoxList.Items.Count);
                Assert.AreEqual("満20才", window.comboBoxList.SelectedItem.ToString());
                Assert.AreEqual("", window.textBoxPreview.Text);
                Assert.IsTrue(window.buttonView.IsDefault);
                Assert.IsTrue(window.buttonOk.IsCancel);
            });
        }

        [TestMethod]
        public void 閲覧基準で表示すると右の欄に結果が出る()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow(2026);

                Click(window.buttonView);

                Assert.AreEqual("2006年生まれ\r\n今年は20才\r\n戌(いぬ)年です。\r\n", window.textBoxPreview.Text);
            });
        }

        [TestMethod]
        public void 干支に切り替えるとリストが12支になり_その干支の人を一覧できる()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow(2026);

                window.radioZodiac.IsChecked = true;
                Assert.AreEqual(12, window.comboBoxList.Items.Count);
                Assert.AreEqual("子(ねずみ)", window.comboBoxList.SelectedItem.ToString());

                Click(window.buttonView);
                StringAssert.StartsWith(window.textBoxPreview.Text, "子(ねずみ)年のヒト\r\n\r\n生まれた年    年齢\r\n1900年        満126才\r\n");
            });
        }

        [TestMethod]
        public void 今年を変えるとリストが作り直され_1980年が無ければ未選択で何も表示しない()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow(2026);
                window.radioBirth.IsChecked = true;
                Assert.AreEqual("1980年", window.comboBoxList.SelectedItem.ToString());

                window.comboBoxYear.SelectedItem = 1950;

                Assert.AreEqual(1950, window.Year);
                Assert.AreEqual(51, window.comboBoxList.Items.Count);
                Assert.AreEqual(-1, window.comboBoxList.SelectedIndex);
                Click(window.buttonView);
                Assert.AreEqual("", window.textBoxPreview.Text);
            });
        }

        [TestMethod]
        public void 生まれ年が1900年より前になる年齢はプログラムエラーを出してから表示する()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow(1950);
                ShownMessage shown = CaptureMessage(window);
                window.comboBoxList.SelectedIndex = 100;

                Click(window.buttonView);

                Assert.AreEqual(1, shown.Count);
                Assert.AreEqual("プログラムエラー", shown.Text);
                Assert.AreEqual("Zodiac", shown.Caption);
                Assert.AreEqual(MessageBoxImage.Exclamation, shown.Icon);
                Assert.AreEqual("1850年生まれ\r\n今年は100才\r\n年です。\r\n", window.textBoxPreview.Text);
            });
        }

        [TestMethod]
        public void 一覧表示は早見表をメッセージボックスで出す()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow(2026);
                ShownMessage shown = CaptureMessage(window);

                Click(window.buttonAllView);

                Assert.AreEqual(1, shown.Count);
                Assert.AreEqual("早見表", shown.Caption);
                Assert.AreEqual(MessageBoxImage.None, shown.Icon);
                Assert.AreEqual(Logic.BuildAllView(2026), shown.Text);
            });
        }
    }
}
