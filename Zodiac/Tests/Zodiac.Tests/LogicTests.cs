using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Zodiac.Tests
{
    /// <summary>
    /// Logic(干支の算出・下段のリスト・右の欄の文言・早見表)のテスト。
    /// </summary>
    [TestClass]
    public class LogicTests
    {
        [TestMethod]
        public void 干支は1900年が子で12年周期()
        {
            string zodiac;

            Assert.IsTrue(Logic.TryGetZodiac(1900, 2026, out zodiac));
            Assert.AreEqual("子(ねずみ)", zodiac);
            Assert.IsTrue(Logic.TryGetZodiac(2026, 2026, out zodiac));
            Assert.AreEqual("午(うま)", zodiac);
            Assert.IsTrue(Logic.TryGetZodiac(1911, 2026, out zodiac));
            Assert.AreEqual("亥(いのしし)", zodiac);
        }

        [TestMethod]
        public void 干支は1900年より前と今年より後は引けない()
        {
            string zodiac;

            Assert.IsFalse(Logic.TryGetZodiac(1899, 2026, out zodiac));
            Assert.AreEqual("", zodiac);
            Assert.IsFalse(Logic.TryGetZodiac(2027, 2026, out zodiac));
        }

        [TestMethod]
        public void 今年の選択肢は1900年から2100年()
        {
            IReadOnlyList<int> years = Logic.YearChoices();

            Assert.AreEqual(201, years.Count);
            Assert.AreEqual(1900, years[0]);
            Assert.AreEqual(2100, years[200]);
        }

        [TestMethod]
        public void 起動時は今年を選び_範囲外なら先頭の1900年()
        {
            Assert.AreEqual(126, Logic.YearIndex(2026));
            Assert.AreEqual(0, Logic.YearIndex(1899));
            Assert.AreEqual(0, Logic.YearIndex(2101));
        }

        [TestMethod]
        public void 年齢のリストは満0才から満130才で満20才を選ぶ()
        {
            int selected;
            List<ListEntry> list = Logic.BuildList(ViewMode.Age, 2026, out selected);

            Assert.AreEqual(131, list.Count);
            Assert.AreEqual("満0才", list[0].Text);
            Assert.AreEqual("満130才", list[130].Text);
            Assert.AreEqual("満20才", list[selected].Text);
            Assert.AreEqual(20, list[selected].Value);
        }

        [TestMethod]
        public void 生まれた年のリストは1900年から今年までで1980年を選ぶ()
        {
            int selected;
            List<ListEntry> list = Logic.BuildList(ViewMode.Birth, 2026, out selected);

            Assert.AreEqual(127, list.Count);
            Assert.AreEqual("1900年", list[0].Text);
            Assert.AreEqual("2026年", list[126].Text);
            Assert.AreEqual("1980年", list[selected].Text);
            Assert.AreEqual(1980, list[selected].Value);
        }

        [TestMethod]
        public void 今年が1980年より前だと生まれた年のリストは未選択になる()
        {
            int selected;
            List<ListEntry> list = Logic.BuildList(ViewMode.Birth, 1950, out selected);

            Assert.AreEqual(51, list.Count);
            Assert.AreEqual(-1, selected);
        }

        [TestMethod]
        public void 干支のリストは12個で子を選ぶ()
        {
            int selected;
            List<ListEntry> list = Logic.BuildList(ViewMode.Zodiac, 2026, out selected);

            Assert.AreEqual(12, list.Count);
            Assert.AreEqual(0, selected);
            Assert.AreEqual("子(ねずみ)", list[0].Text);
            Assert.AreEqual("亥(いのしし)", list[11].Text);
            Assert.AreEqual(11, list[11].Value);
            Assert.AreEqual("亥(いのしし)", list[11].ToString());
        }

        [TestMethod]
        public void 年齢から生まれた年と干支を表示する()
        {
            bool error;
            string view = Logic.BuildView(ViewMode.Age, 20, 2026, out error);

            Assert.IsFalse(error);
            Assert.AreEqual("2006年生まれ\r\n今年は20才\r\n戌(いぬ)年です。\r\n", view);
        }

        [TestMethod]
        public void 生まれた年から年齢と干支を表示する()
        {
            bool error;
            string view = Logic.BuildView(ViewMode.Birth, 1980, 2026, out error);

            Assert.IsFalse(error);
            Assert.AreEqual("1980年生まれ\r\n今年は46才\r\n申(さる)年です。\r\n", view);
        }

        [TestMethod]
        public void 干支からその年生まれの人を一覧する()
        {
            bool error;
            string view = Logic.BuildView(ViewMode.Zodiac, 0, 1930, out error);

            Assert.IsFalse(error);
            Assert.AreEqual(
                "子(ねずみ)年のヒト\r\n\r\n" +
                "生まれた年    年齢\r\n" +
                "1900年        満30才\r\n" +
                "1912年        満18才\r\n" +
                "1924年        満6才\r\n",
                view);
        }

        [TestMethod]
        public void 干支の一覧に今年生まれは含めない()
        {
            bool error;
            string view = Logic.BuildView(ViewMode.Zodiac, 6, 2026, out error);

            StringAssert.Contains(view, "2014年        満12才\r\n");
            Assert.IsFalse(view.Contains("2026年"));
        }

        [TestMethod]
        public void 生まれ年が1900年より前になる年齢はプログラムエラーで干支は空()
        {
            bool error;
            string view = Logic.BuildView(ViewMode.Age, 100, 1950, out error);

            Assert.IsTrue(error);
            Assert.AreEqual("1850年生まれ\r\n今年は100才\r\n年です。\r\n", view);
        }

        [TestMethod]
        public void 桁揃えは全角を2_半角を1として数える()
        {
            Assert.AreEqual("    子(ねずみ)", Logic.PadLeftByWidth("子(ねずみ)", 14));
            Assert.AreEqual("  亥(いのしし)", Logic.PadLeftByWidth("亥(いのしし)", 14));
            Assert.AreEqual("       1900", Logic.PadLeftByWidth("1900", 11));
            Assert.AreEqual("ABC", Logic.PadLeftByWidth("ABC", 2));
        }

        [TestMethod]
        public void 早見表は干支の見出しの下に12年ずつ並べる()
        {
            string table = Logic.BuildAllView(1913);

            StringAssert.StartsWith(table, "    子(ねずみ)      丑(うし)");
            StringAssert.EndsWith(table,
                "  亥(いのしし)\r\n" +
                "\r\n       1900年       1901年       1902年       1903年       1904年       1905年" +
                "       1906年       1907年       1908年       1909年       1910年       1911年" +
                "\r\n       1912年       1913年");
        }
    }
}
