using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StandardTemplate.Wpf.Tests;

namespace TrimHtmlData.Tests
{
    /// <summary>
    /// MainWindow（URLのHTMLから検索文字を含む行を取り出すツール）を実際に生成して確かめるテスト。
    /// HTML取得は差し替えて実サイトへは通信せず、クリップボードへのコピーもテスト用の処理に差し替える。
    /// データ保存先は一時フォルダに差し替え、Close()も呼ばない(本物の設定ファイルを書き換えないため)。
    ///
    /// ⚠️ 余白のダブルクリック(デバッグ切替)と設定保存ボタンは MessageBox / 保存ダイアログが出るため、
    /// 切替そのもの(ToggleDebugMode)だけを確かめる。
    /// </summary>
    [TestClass]
    public class MainWindowTests
    {
        private string tempDirectory;

        [TestInitialize]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "TrimHtmlDataTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
        }

        [TestCleanup]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
            catch (IOException)
            {
                // 後片付けの失敗はテストの成否に関係ないので黙って流す
            }
        }

        [TestMethod]
        public void 実行でURLごとに見出しと該当行を並べクリップボードにも送る()
        {
            var pages = new Dictionary<string, string>
            {
                { "https://a.example/", "<html>\r\n<title>A page</title>\r\n<body>a</body>" },
                { "https://b.example/", "<html>\r\n<title>B page</title>\r\n<body>b</body>" },
            };

            StaRunner.Run(() =>
            {
                var window = new MainWindow(tempDirectory);
                var fetched = new List<string>();
                string copied = null;
                window.FetchHtmlSource = url => { fetched.Add(url); return pages[url]; };
                window.CopyToClipboard = text => { copied = text; return true; };

                // 空行は無視される
                window.textBox_SourceList.Text = "https://a.example/\r\n\r\nhttps://b.example/";
                window.textBox_SearchWord.Text = "<title>";
                window.textBox_TrimLineNum.Text = "2";

                window.ExecuteTrim();

                CollectionAssert.AreEqual(new[] { "https://a.example/", "https://b.example/" }, fetched);
                string expected =
                    "◆https://a.example/\r\n<title>A page</title>\r\n<body>a</body>\r\n\r\n\r\n" +
                    "◆https://b.example/\r\n<title>B page</title>\r\n<body>b</body>\r\n\r\n\r\n";
                Assert.AreEqual(expected, window.textBox_DestList.Text);
                Assert.AreEqual(expected, copied);
            });
        }

        [TestMethod]
        public void 大文字小文字の区別と最初のヒットだけのチェックが反映される()
        {
            StaRunner.Run(() =>
            {
                var window = new MainWindow(tempDirectory);
                window.FetchHtmlSource = url => "<TITLE>upper</TITLE>\r\n<title>one</title>\r\n<title>two</title>";
                window.CopyToClipboard = text => true;

                window.textBox_SourceList.Text = "https://a.example/";
                window.textBox_SearchWord.Text = "<title>";
                window.checkBox_OrdinalCase.IsChecked = true;
                window.checkBox_FirstWordOnly.IsChecked = true;

                window.ExecuteTrim();

                Assert.AreEqual("◆https://a.example/\r\n<title>one</title>\r\n\r\n", window.textBox_DestList.Text);
            });
        }

        [TestMethod]
        public void 余白ダブルクリックのデバッグ切替は押すたびに反転する()
        {
            StaRunner.Run(() =>
            {
                var window = new MainWindow(tempDirectory);
                Assert.IsFalse(window.IsDebug);

                window.ToggleDebugMode();
                Assert.IsTrue(window.IsDebug);

                window.ToggleDebugMode();
                Assert.IsFalse(window.IsDebug);
            });
        }

        [TestMethod]
        public void 検索文字終端の欄はWinForms版と同じく非表示になっている()
        {
            StaRunner.Run(() =>
            {
                var window = new MainWindow(tempDirectory);

                Assert.AreEqual("TrimHtmlData", window.Title);
                Assert.AreEqual(Visibility.Collapsed, window.textBox_DelimiterWord.Visibility);
                Assert.AreEqual(Visibility.Collapsed, window.label_DelimiterWord.Visibility);
                Assert.AreEqual(Visibility.Visible, window.textBox_SearchWord.Visibility);
                Assert.AreEqual("各URLで最初にヒットした箇所だけを取り出す", window.checkBox_FirstWordOnly.ToolTip);
            });
        }
    }
}
