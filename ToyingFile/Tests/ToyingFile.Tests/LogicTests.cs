using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ToyingFile.Tests
{
    /// <summary>
    /// Logic（ファイル内容から指定文字列を含む行を処理するロジック）のテスト。
    /// DeleteStringFromContentのテストはWinForms版のLogicTestsをそのまま流用し、
    /// WPF版で画面側から切り出したGetTargetFiles/SplitDeleteStringsのテストを足している。
    /// </summary>
    [TestClass]
    public class LogicTests
    {
        [TestMethod]
        public void 該当行だけ文字列を削除する()
        {
            string content = "keep this" + Environment.NewLine + "delete THIS word" + Environment.NewLine + "keep too";

            string result = Logic.DeleteStringFromContent(content, new[] { "THIS" }, caseSensitive: true, deleteWholeLine: false);

            Assert.AreEqual("keep this" + Environment.NewLine + "delete  word" + Environment.NewLine + "keep too", result);
        }

        [TestMethod]
        public void deleteWholeLineがtrueなら行ごと空行になる()
        {
            string content = "keep this" + Environment.NewLine + "delete this line" + Environment.NewLine + "keep too";

            string result = Logic.DeleteStringFromContent(content, new[] { "delete" }, caseSensitive: true, deleteWholeLine: true);

            Assert.AreEqual("keep this" + Environment.NewLine + "" + Environment.NewLine + "keep too", result);
        }

        [TestMethod]
        public void caseSensitiveがtrueなら大文字小文字を区別して検索する()
        {
            string content = "target line" + Environment.NewLine + "TARGET line";

            string result = Logic.DeleteStringFromContent(content, new[] { "target" }, caseSensitive: true, deleteWholeLine: true);

            // 1行目だけ完全一致するので空行に、2行目(大文字)は対象外でそのまま残る
            Assert.AreEqual("" + Environment.NewLine + "TARGET line", result);
        }

        [TestMethod]
        public void caseSensitiveがfalseなら大文字小文字を区別せず検索する()
        {
            string content = "target line" + Environment.NewLine + "TARGET line";

            string result = Logic.DeleteStringFromContent(content, new[] { "target" }, caseSensitive: false, deleteWholeLine: true);

            // どちらも検索には一致するので両方空行になる
            Assert.AreEqual("" + Environment.NewLine + "", result);
        }

        [TestMethod]
        public void 大文字小文字を区別しない指定なら大文字の文字列も削除される()
        {
            // 以前は検索(IndexOf)だけ大文字小文字を無視し、削除(String.Replace)は区別していたため
            // 大文字側が消えずに残っていた。Regex.Replaceに変えて判定と削除の挙動を揃えた。
            string content = "TARGET line";

            string result = Logic.DeleteStringFromContent(content, new[] { "target" }, caseSensitive: false, deleteWholeLine: false);

            Assert.AreEqual(" line", result, "検索がヒットした文字列は削除される");
        }

        [TestMethod]
        public void 空行は判定をスキップする()
        {
            string content = "" + Environment.NewLine + "keep this";

            string result = Logic.DeleteStringFromContent(content, new[] { "keep" }, caseSensitive: true, deleteWholeLine: true);

            Assert.AreEqual("" + Environment.NewLine + "", result);
        }

        [TestMethod]
        public void 複数の削除対象文字列を扱える()
        {
            string content = "foo bar baz";

            string result = Logic.DeleteStringFromContent(content, new[] { "foo", "baz" }, caseSensitive: true, deleteWholeLine: false);

            Assert.AreEqual(" bar ", result);
        }

        [TestMethod]
        public void SplitDeleteStrings_1行1件で空行は無視する()
        {
            string[] result = Logic.SplitDeleteStrings("foo" + Environment.NewLine + Environment.NewLine + "bar" + Environment.NewLine);

            CollectionAssert.AreEqual(new[] { "foo", "bar" }, result);
        }

        [TestMethod]
        public void GetTargetFiles_パターン空なら全ファイルでサブフォルダ指定に従う()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ToyingFileLogicTests_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "sub"));
                File.WriteAllText(Path.Combine(dir, "a.txt"), "");
                File.WriteAllText(Path.Combine(dir, "b.log"), "");
                File.WriteAllText(Path.Combine(dir, "sub", "c.txt"), "");

                string[] top = Logic.GetTargetFiles(dir, "", false).Select(Path.GetFileName).OrderBy(n => n).ToArray();
                string[] all = Logic.GetTargetFiles(dir, "", true).Select(Path.GetFileName).OrderBy(n => n).ToArray();
                string[] txt = Logic.GetTargetFiles(dir, "*.txt", true).Select(Path.GetFileName).OrderBy(n => n).ToArray();

                CollectionAssert.AreEqual(new[] { "a.txt", "b.log" }, top);
                CollectionAssert.AreEqual(new[] { "a.txt", "b.log", "c.txt" }, all);
                CollectionAssert.AreEqual(new[] { "a.txt", "c.txt" }, txt);
            }
            finally
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                    // 後片付けの失敗はテストの成否に関係ないので黙って流す
                }
            }
        }
    }
}
