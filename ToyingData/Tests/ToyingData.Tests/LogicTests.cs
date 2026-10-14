using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ToyingData.Tests
{
    /// <summary>
    /// Logic（全角→半角変換のロジック）のテスト。WinForms版のLogicTestsをそのまま流用している。
    /// </summary>
    [TestClass]
    public class LogicTests
    {
        [TestMethod]
        public void TryGetRegexPattern_何も選ばれていなければfalse()
        {
            string dummy;
            bool result = Logic.TryGetRegexPattern(false, false, false, false, out dummy);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TryGetRegexPattern_数字のみ選ぶとその範囲だけ含む()
        {
            string result;
            Logic.TryGetRegexPattern(true, false, false, false, out result);

            Assert.AreEqual("[０-９]", result);
        }

        [TestMethod]
        public void TryGetRegexPattern_複数選ぶと結合される()
        {
            string result;
            bool success = Logic.TryGetRegexPattern(true, true, false, true, out result);

            Assert.IsTrue(success);
            Assert.AreEqual("[０-９Ａ-Ｚ　]", result);
        }

        [TestMethod]
        public void ApplyWide2Narrow_全角数字を半角にする()
        {
            string regexPattern;
            Logic.TryGetRegexPattern(true, false, false, false, out regexPattern);

            string[] result = Logic.ApplyWide2Narrow(new[] { "１２３", "abc" }, regexPattern);

            Assert.AreEqual("123", result[0]);
            Assert.AreEqual("abc", result[1], "対象外の文字はそのまま");
        }

        [TestMethod]
        public void ApplyWide2Narrow_対象文字が無ければ変化しない()
        {
            string regexPattern;
            Logic.TryGetRegexPattern(true, false, false, false, out regexPattern);

            string[] result = Logic.ApplyWide2Narrow(new[] { "あいう" }, regexPattern);

            Assert.AreEqual("あいう", result[0]);
        }
    }
}
