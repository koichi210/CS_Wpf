using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TurnMemory.Tests
{
    /// <summary>
    /// Logic(出題順の割り当て・順番表示とカウントダウンの進行・答え合わせ)のテスト。
    /// </summary>
    [TestClass]
    public class LogicTests
    {
        // Next(max)が常に max-1 を返す乱数。Fisher-Yatesで入れ替えが起きず、左上から 1,2,3… の並びになる
        internal sealed class NoSwapRandom : Random
        {
            public override int Next(int maxValue)
            {
                return maxValue - 1;
            }
        }

        private static Logic StartedLogic(int size)
        {
            Logic logic = new Logic(new NoSwapRandom()) { Size = size };
            logic.Start();
            return logic;
        }

        [TestMethod]
        public void マス数の選択肢は3x3から10x10までの8通り()
        {
            IReadOnlyList<string> choices = Logic.SizeChoices();

            Assert.AreEqual(8, choices.Count);
            Assert.AreEqual("3 × 3", choices[0]);
            Assert.AreEqual("4 × 4", choices[1]);
            Assert.AreEqual("10 × 10", choices[7]);
        }

        [TestMethod]
        public void 選択肢の番号とマス数を相互に変換できる()
        {
            Assert.AreEqual(1, Logic.SizeToIndex(Logic.DefaultSize));
            Assert.AreEqual(3, Logic.IndexToSize(0));
            Assert.AreEqual(10, Logic.IndexToSize(7));
        }

        [TestMethod]
        public void 行の見出しはAからJ_列の見出しは1から10()
        {
            Assert.AreEqual("A", Logic.RowLabel(0));
            Assert.AreEqual("J", Logic.RowLabel(9));
            Assert.AreEqual("1", Logic.ColLabel(0));
            Assert.AreEqual("10", Logic.ColLabel(9));
        }

        [TestMethod]
        public void 初期状態は4x4で次の順番は1()
        {
            Logic logic = new Logic(new Random(0));

            Assert.AreEqual(4, logic.Size);
            Assert.AreEqual(16, logic.CellCount);
            Assert.AreEqual(GameState.Init, logic.State);
            Assert.AreEqual(1, logic.NextNumber);
        }

        [TestMethod]
        public void マス数は3から10の範囲外を受け付けない()
        {
            Logic logic = new Logic(new Random(0));

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => logic.Size = 2);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => logic.Size = 11);
        }

        [TestMethod]
        public void 出題すると全マスに1からマス数までの順番が重複なく割り当てられる()
        {
            for (int size = Logic.CellMin; size <= Logic.CellMax; size++)
            {
                Logic logic = new Logic(new Random(size)) { Size = size };
                logic.Start();

                int[] answers = Enumerable.Range(0, logic.CellCount).Select(logic.GetAnswer).OrderBy(n => n).ToArray();

                CollectionAssert.AreEqual(Enumerable.Range(1, size * size).ToArray(), answers, "size=" + size);
                Assert.AreEqual(GameState.Start, logic.State);
            }
        }

        [TestMethod]
        public void 乱数を差し替えると出題順を固定できる()
        {
            Logic logic = StartedLogic(3);

            for (int i = 0; i < 9; i++)
            {
                Assert.AreEqual(i + 1, logic.GetAnswer(i));
            }
        }

        [TestMethod]
        public void 順番表示は1から順にマスを返し最後の1つで記憶時間に移る()
        {
            Logic logic = StartedLogic(3);
            bool finished;

            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(i, logic.ShowNext(out finished));
                Assert.IsFalse(finished);
            }
            Assert.AreEqual(8, logic.ShowNext(out finished));

            Assert.IsTrue(finished);
            Assert.AreEqual(1, logic.NextNumber);
            Assert.AreEqual(Logic.WaitMax, logic.Wait);
            Assert.AreEqual(GameState.Start, logic.State);
        }

        [TestMethod]
        public void 記憶時間は5から0まで数えてから解答の入力に移る()
        {
            Logic logic = StartedLogic(3);
            bool finished;
            for (int i = 0; i < 9; i++)
            {
                logic.ShowNext(out finished);
            }

            List<int> shown = new List<int>();
            do
            {
                shown.Add(logic.WaitTick(out finished));
            } while (!finished);

            CollectionAssert.AreEqual(new[] { 5, 4, 3, 2, 1, 0 }, shown);
            Assert.AreEqual(GameState.End, logic.State);
            Assert.AreEqual(Logic.WaitMax, logic.Wait);
        }

        [TestMethod]
        public void カウントダウンの文言()
        {
            Assert.AreEqual("5秒間覚えて下さい。", Logic.RememberText(5));
            Assert.AreEqual("0秒間覚えて下さい。", Logic.RememberText(0));
        }

        [TestMethod]
        public void ストップして打ち切ると正解が消えて次の順番も1に戻る()
        {
            Logic logic = StartedLogic(3);
            bool finished;
            logic.ShowNext(out finished);
            logic.ShowNext(out finished);

            logic.Stop();
            Assert.AreEqual(GameState.Stop, logic.State);
            logic.Exit();

            Assert.AreEqual(GameState.End, logic.State);
            Assert.AreEqual(1, logic.NextNumber);
            for (int i = 0; i < logic.CellCount; i++)
            {
                Assert.AreEqual(0, logic.GetAnswer(i));
            }
        }

        [TestMethod]
        public void 出題を始めると前回の入力は消える()
        {
            Logic logic = StartedLogic(3);
            logic.SetInput(0, 5);

            logic.Start();

            Assert.AreEqual(0, logic.GetInput(0));
        }

        [TestMethod]
        public void 入力欄の文字は数値として読み_読めなければ0()
        {
            Assert.AreEqual(12, Logic.ParseInput("12"));
            Assert.AreEqual(0, Logic.ParseInput(""));
            Assert.AreEqual(0, Logic.ParseInput(null));
            Assert.AreEqual(0, Logic.ParseInput("abc"));
            Assert.AreEqual(0, Logic.ParseInput("-3"));
        }

        [TestMethod]
        public void 全マス一致なら全問正解()
        {
            Logic logic = StartedLogic(3);
            for (int i = 0; i < logic.CellCount; i++)
            {
                logic.SetInput(i, logic.GetAnswer(i));
            }

            Assert.IsTrue(logic.IsAllCorrect());
            Assert.AreEqual("全問正解！！", logic.JudgeTitle());
        }

        [TestMethod]
        public void 一つでも違えば残念で_違うマスだけ不正解になる()
        {
            Logic logic = StartedLogic(3);
            for (int i = 0; i < logic.CellCount; i++)
            {
                logic.SetInput(i, logic.GetAnswer(i));
            }
            logic.SetInput(4, 0);

            Assert.IsFalse(logic.IsAllCorrect());
            Assert.IsFalse(logic.IsCorrect(4));
            Assert.IsTrue(logic.IsCorrect(3));
            Assert.AreEqual("残念。。", logic.JudgeTitle());
        }
    }
}
