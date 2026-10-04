using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace CubNotice.Tests
{
    [TestClass]
    public class PdfExtractTests
    {
        private static readonly string FontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "yumin.ttf");

        /// <summary>
        /// 日本語フォントを埋め込んだ2ページのPDFを作り、2ページ目から予定を取り込めることを確かめる。
        /// </summary>
        [TestMethod]
        public void ExtractLines_2ページ目の行を取り出して解析できる()
        {
            if (!File.Exists(FontPath))
            {
                Assert.Inconclusive("游明朝(yumin.ttf)が無い環境なのでスキップ");
            }
            string pdfPath = Path.Combine(Path.GetTempPath(), "CubNoticeTest_" + Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
                File.WriteAllBytes(pdfPath, BuildPdf());

                Assert.AreEqual(2, PdfTextExtractor.GetPageCount(pdfPath));
                List<string> lines = PdfTextExtractor.ExtractLines(pdfPath, 2);
                ParseResult result = NewsletterParser.Parse(lines, "ヘッダ", new DateTime(2026, 10, 4));

                Assert.AreEqual(new DateTime(2026, 9, 27), result.IssueDate, string.Join("\n", lines));
                Assert.AreEqual(3, result.Events.Count, string.Join("\n", lines));
                CubEvent first = result.Events[0];
                Assert.AreEqual("隊集会 赤い羽根共同募金＆カントリー大作戦", first.Title);
                Assert.AreEqual("13時00分  Ａ駅南口藤棚下", first.Gathering);
                Assert.AreEqual("15時00分  Ａ駅南口藤棚下（予定）", first.Dismissal);
                Assert.AreEqual(2, first.Notes.Split('\n').Length);
            }
            finally
            {
                File.Delete(pdfPath);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void ExtractLines_無いページはエラー()
        {
            string pdfPath = Path.Combine(Path.GetTempPath(), "CubNoticeTest_" + Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
                PdfDocumentBuilder builder = new PdfDocumentBuilder();
                PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);
                builder.AddPage(595, 842).AddText("page1", 12, new PdfPoint(50, 800), font);
                File.WriteAllBytes(pdfPath, builder.Build());

                PdfTextExtractor.ExtractLines(pdfPath, 2);
            }
            finally
            {
                File.Delete(pdfPath);
            }
        }

        private static byte[] BuildPdf()
        {
            PdfDocumentBuilder builder = new PdfDocumentBuilder();
            PdfDocumentBuilder.AddedFont font = builder.AddTrueTypeFont(File.ReadAllBytes(FontPath));

            PdfPageBuilder page1 = builder.AddPage(595, 842);
            page1.AddText("カブ8通信 10月号", 12, new PdfPoint(50, 800), font);

            PdfPageBuilder page2 = builder.AddPage(595, 842);
            double y = 810;
            foreach (string line in SampleText.Page2.Replace("\r\n", "\n").Split('\n'))
            {
                // 行頭の「集 合」などは字下げされているのでX座標をずらす
                bool indented = !line.StartsWith("☆") && !line.StartsWith("≪") && !line.Contains("発行");
                double x = indented ? 70 : 40;
                // 単語ごとに配置し、空白の位置で隙間を空ける(PDF上の見た目と同じく空白文字は置かない)
                foreach (string word in line.Split(' ').Where(w => w.Length > 0))
                {
                    page2.AddText(word, 10, new PdfPoint(x, y), font);
                    x += page2.MeasureText(word, 10, new PdfPoint(x, y), font).Sum(l => l.Width) + 6;
                }
                y -= 16;
            }
            return builder.Build();
        }
    }
}
