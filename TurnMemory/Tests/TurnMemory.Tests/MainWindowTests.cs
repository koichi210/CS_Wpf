using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StandardTemplate.Wpf.Tests;

namespace TurnMemory.Tests
{
    /// <summary>
    /// 出題画面・解答画面を実際に生成して、ボタン操作とタイマ1回分の処理で画面がMFC版と同じように変わるかを確認するテスト。
    /// タイマは待たずに ShowProc/WaitProc を直接呼んで進める。保存する設定は無いが、念のため Close() はしない(フォーカスを見るために Show() したテストだけ最後に閉じる)。
    /// </summary>
    [TestClass]
    public class MainWindowTests
    {
        private static void Click(ButtonBase button)
        {
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }

        [TestMethod]
        public void 起動直後は4x4のマスが空で入力できず_御明算は押せない()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow(new LogicTests.NoSwapRandom());

                Assert.AreEqual("順番記憶", window.Title);
                Assert.AreEqual("スタートを押して下さい。", window.textTitle.Text);
                Assert.AreEqual(8, window.comboBoxProblem.Items.Count);
                Assert.AreEqual("4 × 4", window.comboBoxProblem.SelectedItem);
                Assert.AreEqual("スタート", window.buttonStart.Content);
                Assert.IsFalse(window.buttonAns.IsEnabled);
                Assert.IsFalse(window.buttonOk.IsDefault);
                Assert.IsTrue(window.buttonOk.IsCancel);

                Assert.AreEqual(Visibility.Visible, window.Cell(3, 3).Visibility);
                Assert.IsFalse(window.Cell(3, 3).IsEnabled);
                Assert.AreEqual("", window.Cell(3, 3).Text);
                Assert.AreEqual(Visibility.Hidden, window.Cell(0, 4).Visibility);
                Assert.AreEqual(Visibility.Visible, window.Cells.RowLabel(3).Visibility);
                Assert.AreEqual(Visibility.Hidden, window.Cells.RowLabel(4).Visibility);
                Assert.AreEqual(Visibility.Hidden, window.Cells.ColLabel(4).Visibility);
            });
        }

        [TestMethod]
        public void スタートで出題が始まりストップに変わる()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow(new LogicTests.NoSwapRandom());

                Click(window.buttonStart);

                Assert.AreEqual("ストップ", window.buttonStart.Content);
                Assert.AreEqual("順番を記憶して下さい。", window.textTitle.Text);
                Assert.IsFalse(window.comboBoxProblem.IsEnabled);
                Assert.IsFalse(window.buttonAns.IsEnabled);
                Assert.AreEqual(GameState.Start, window.GameLogic.State);
            });
        }

        [TestMethod]
        public void 順番を全部表示してカウントダウンが終わると解答を入力できる()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow(new LogicTests.NoSwapRandom());
                Click(window.buttonStart);

                window.ShowProc();
                Assert.AreEqual("1", window.Cell(0, 0).Text);
                for (int i = 1; i < 16; i++)
                {
                    window.ShowProc();
                }
                Assert.AreEqual("16", window.Cell(3, 3).Text);
                Assert.AreEqual("5秒間覚えて下さい。", window.textTitle.Text);

                for (int i = 0; i < 4; i++)
                {
                    window.WaitProc();
                }
                Assert.AreEqual("1秒間覚えて下さい。", window.textTitle.Text);
                window.WaitProc();

                Assert.AreEqual("解答を入力して下さい。", window.textTitle.Text);
                Assert.AreEqual("スタート", window.buttonStart.Content);
                Assert.IsTrue(window.buttonAns.IsEnabled);
                Assert.IsTrue(window.comboBoxProblem.IsEnabled);
                Assert.IsTrue(window.Cell(0, 0).IsEnabled);
                Assert.AreEqual("", window.Cell(0, 0).Text);
                Assert.AreEqual(GameState.End, window.GameLogic.State);
            });
        }

        [TestMethod]
        public void 出題中にストップすると打ち切られ御明算は押せないまま()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow(new LogicTests.NoSwapRandom());
                Click(window.buttonStart);
                window.ShowProc();
                window.ShowProc();

                Click(window.buttonStart);

                Assert.AreEqual("スタートを押して下さい。", window.textTitle.Text);
                Assert.AreEqual("スタート", window.buttonStart.Content);
                Assert.IsFalse(window.buttonAns.IsEnabled);
                Assert.IsTrue(window.comboBoxProblem.IsEnabled);
                Assert.IsTrue(window.Cell(0, 0).IsEnabled);
                Assert.AreEqual("", window.Cell(0, 0).Text);
                Assert.AreEqual(0, window.GameLogic.GetAnswer(0));
            });
        }

        [TestMethod]
        public void マス数の変更は次のスタートで反映される()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow(new LogicTests.NoSwapRandom());

                window.comboBoxProblem.SelectedIndex = Logic.SizeToIndex(3);
                Assert.AreEqual(Visibility.Visible, window.Cell(3, 3).Visibility);

                Click(window.buttonStart);
                Assert.AreEqual(Visibility.Hidden, window.Cell(3, 3).Visibility);
                Assert.AreEqual(Visibility.Visible, window.Cell(2, 2).Visibility);
                Assert.AreEqual(3, window.GameLogic.Size);
            });
        }

        [TestMethod]
        public void 解答画面は正解の順番を表示し_違うマスを赤字にする()
        {
            StaRunner.Run(() =>
            {
                Logic logic = new Logic(new LogicTests.NoSwapRandom()) { Size = 3 };
                logic.Start();
                for (int i = 0; i < logic.CellCount; i++)
                {
                    logic.SetInput(i, logic.GetAnswer(i));
                }
                logic.SetInput(4, 7);

                AnsWindow window = new AnsWindow(logic);

                Assert.AreEqual("解答", window.Title);
                Assert.AreEqual("残念。。", window.textTitle.Text);
                Assert.AreEqual("閉じれ", window.buttonOk.Content);
                Assert.AreEqual("5", window.CellText(1, 1).Text);
                Assert.AreEqual(Brushes.Red, window.CellText(1, 1).Foreground);
                Assert.AreEqual("4", window.CellText(1, 0).Text);
                Assert.AreNotEqual(Brushes.Red, window.CellText(1, 0).Foreground);
            });
        }

        [TestMethod]
        public void 解答画面は全部合っていれば全問正解()
        {
            StaRunner.Run(() =>
            {
                Logic logic = new Logic(new LogicTests.NoSwapRandom()) { Size = 4 };
                logic.Start();
                for (int i = 0; i < logic.CellCount; i++)
                {
                    logic.SetInput(i, logic.GetAnswer(i));
                }

                AnsWindow window = new AnsWindow(logic);

                Assert.AreEqual("全問正解！！", window.textTitle.Text);
                Assert.AreEqual("16", window.CellText(3, 3).Text);
            });
        }

        [TestMethod]
        public void マスでEnterを押すと次のマスへ進み_画面は閉じない()
        {
            StaRunner.Run(() =>
            {
                MainWindow window = new MainWindow(new LogicTests.NoSwapRandom());
                // フォーカスを動かすには画面が表示されている必要がある。スタート→ストップでマスを入力可能にする
                window.Show();
                Click(window.buttonStart);
                Click(window.buttonStart);
                window.Cell(0, 0).Focus();

                KeyEventArgs enter = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Enter)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                };
                window.Cell(0, 0).RaiseEvent(enter);

                Assert.IsTrue(enter.Handled);
                Assert.IsTrue(window.Cell(0, 1).IsKeyboardFocused);
                Assert.IsTrue(window.IsVisible);
                window.Close();
            });
        }
    }
}
