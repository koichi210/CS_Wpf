using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TrimFileData.Tests
{
    /// <summary>
    /// Logic（WinForms版のForm1.cs から切り出した、検索ワードリストとリファレンスデータから
    /// 該当行を抽出するロジック）のテスト。
    /// </summary>
    [TestClass]
    public class LogicTests
    {
        [TestMethod]
        public void 検索ワードにヒットした行を抽出する()
        {
            string[] source = { "apple" };
            string[] refer = { "I like apple pie", "I like banana" };

            string result = Logic.GetSearchData(source, refer, ordinalCase: false, firstWordOnly: false, searchCommonWord: "");

            StringAssert.Contains(result, "◆apple");
            StringAssert.Contains(result, "I like apple pie");
            Assert.IsFalse(result.Contains("banana"));
        }

        [TestMethod]
        public void SplitLinesは改行コードを問わず行に分け空行は捨てる()
        {
            CollectionAssert.AreEqual(new[] { "a", "b", "c", "d" }, Logic.SplitLines("a\r\nb\nc\r\r\nd\n"));
        }

        [TestMethod]
        public void ordinalCaseがtrueなら大文字小文字を区別する()
        {
            string[] source = { "APPLE" };
            string[] refer = { "apple pie" };

            string result = Logic.GetSearchData(source, refer, ordinalCase: true, firstWordOnly: false, searchCommonWord: "");

            Assert.IsFalse(result.Contains("apple pie"));
        }

        [TestMethod]
        public void ordinalCaseがfalseなら大文字小文字を区別しない()
        {
            string[] source = { "APPLE" };
            string[] refer = { "apple pie" };

            string result = Logic.GetSearchData(source, refer, ordinalCase: false, firstWordOnly: false, searchCommonWord: "");

            StringAssert.Contains(result, "apple pie");
        }

        [TestMethod]
        public void firstWordOnlyがtrueなら最初の一致だけ抽出する()
        {
            string[] source = { "apple" };
            string[] refer = { "apple one", "apple two", "apple three" };

            string result = Logic.GetHitLines(new[] { "apple" }, refer, StringComparison.OrdinalIgnoreCase, firstWordOnly: true, searchCommonWord: "");

            StringAssert.Contains(result, "apple one");
            Assert.IsFalse(result.Contains("apple two"));
            Assert.IsFalse(result.Contains("apple three"));
        }

        [TestMethod]
        public void firstWordOnlyがfalseなら全ての一致を抽出する()
        {
            string[] refer = { "apple one", "apple two" };

            string result = Logic.GetHitLines(new[] { "apple" }, refer, StringComparison.OrdinalIgnoreCase, firstWordOnly: false, searchCommonWord: "");

            StringAssert.Contains(result, "apple one");
            StringAssert.Contains(result, "apple two");
        }

        [TestMethod]
        public void searchCommonWordを含まない行は除外される()
        {
            string[] refer = { "apple + common", "apple only" };

            string result = Logic.GetHitLines(new[] { "apple" }, refer, StringComparison.OrdinalIgnoreCase, firstWordOnly: false, searchCommonWord: "common");

            StringAssert.Contains(result, "apple + common");
            Assert.IsFalse(result.Contains("apple only"));
        }

        [TestMethod]
        public void searchCommonWordが空なら絞り込みしない()
        {
            string[] refer = { "apple + common", "apple only" };

            string result = Logic.GetHitLines(new[] { "apple" }, refer, StringComparison.OrdinalIgnoreCase, firstWordOnly: false, searchCommonWord: "");

            StringAssert.Contains(result, "apple + common");
            StringAssert.Contains(result, "apple only");
        }

        [TestMethod]
        public void 複数の検索ワードをまとめて処理できる()
        {
            string[] source = { "apple banana" };
            string[] refer = { "I have apple", "I have banana", "I have orange" };

            string result = Logic.GetSearchData(source, refer, ordinalCase: false, firstWordOnly: false, searchCommonWord: "");

            StringAssert.Contains(result, "I have apple");
            StringAssert.Contains(result, "I have banana");
            Assert.IsFalse(result.Contains("orange"));
        }
    }
}
