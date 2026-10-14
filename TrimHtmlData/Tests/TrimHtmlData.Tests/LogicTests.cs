using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TrimHtmlData.Tests
{
    /// <summary>
    /// Logic（WinForms版のForm1.cs から切り出した、HTMLソースから検索ワードにヒットする行を
    /// 抽出するロジック）のテスト。WinForms版のテストをそのまま持ってきたもの。
    ///
    /// 実行ボタン全体の流れ（URLごとの見出し・クリップボード）は、HTML取得を差し替えて
    /// MainWindowTests で確かめる（実サイトへは通信しない）。
    /// </summary>
    [TestClass]
    public class LogicTests
    {
        [TestMethod]
        public void GetTrimLineは数値文字列をそのまま返す()
        {
            Assert.AreEqual(3, Logic.GetTrimLine("3"));
        }

        [TestMethod]
        public void GetTrimLineは0や空文字なら1を返す()
        {
            Assert.AreEqual(1, Logic.GetTrimLine("0"));
            Assert.AreEqual(1, Logic.GetTrimLine(""));
        }

        [TestMethod]
        public void ヒットした行を抽出する()
        {
            string source = "line0" + Environment.NewLine + "hit target here" + Environment.NewLine + "line2";

            string result = Logic.GetSearchString(source, "target", 1, StringComparison.OrdinalIgnoreCase, firstWordOnly: false);

            StringAssert.Contains(result, "hit target here");
        }

        [TestMethod]
        public void TrimLineNumで前後の行もまとめて取得する()
        {
            string source = "hit target here" + Environment.NewLine + "next line" + Environment.NewLine + "extra line";

            string result = Logic.GetSearchString(source, "target", 2, StringComparison.OrdinalIgnoreCase, firstWordOnly: false);

            StringAssert.Contains(result, "hit target here");
            StringAssert.Contains(result, "next line");
        }

        [TestMethod]
        public void firstWordOnlyがtrueなら最初のヒットだけで打ち切る()
        {
            string source = "hit target one" + Environment.NewLine + "hit target two";

            string result = Logic.GetSearchString(source, "target", 1, StringComparison.OrdinalIgnoreCase, firstWordOnly: true);

            StringAssert.Contains(result, "hit target one");
            Assert.IsFalse(result.Contains("hit target two"));
        }

        [TestMethod]
        public void firstWordOnlyがfalseなら全てのヒットを取得する()
        {
            string source = "hit target one" + Environment.NewLine + "hit target two";

            string result = Logic.GetSearchString(source, "target", 1, StringComparison.OrdinalIgnoreCase, firstWordOnly: false);

            StringAssert.Contains(result, "hit target one");
            StringAssert.Contains(result, "hit target two");
        }

        [TestMethod]
        public void CmpOptがOrdinalなら大文字小文字を区別する()
        {
            string source = "TARGET line";

            string result = Logic.GetSearchString(source, "target", 1, StringComparison.Ordinal, firstWordOnly: false);

            Assert.IsFalse(result.Contains("TARGET line"));
        }

        [TestMethod]
        public void CmpOptがOrdinalIgnoreCaseなら大文字小文字を区別しない()
        {
            string source = "TARGET line";

            string result = Logic.GetSearchString(source, "target", 1, StringComparison.OrdinalIgnoreCase, firstWordOnly: false);

            StringAssert.Contains(result, "TARGET line");
        }

        [TestMethod]
        public void ヒットしなければ空行のみ返す()
        {
            string source = "nothing here";

            string result = Logic.GetSearchString(source, "target", 1, StringComparison.OrdinalIgnoreCase, firstWordOnly: false);

            Assert.AreEqual(Environment.NewLine, result);
        }

        [TestMethod]
        public void 末尾付近のヒットで取得行数が足りなくても例外にならずある分だけ返す()
        {
            string source = "line0" + Environment.NewLine + "hit target last";

            string result = Logic.GetSearchString(source, "target", 5, StringComparison.OrdinalIgnoreCase, firstWordOnly: false);

            Assert.AreEqual("hit target last" + Environment.NewLine + Environment.NewLine + Environment.NewLine, result);
        }
    }
}
