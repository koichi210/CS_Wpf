using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CubNotice.Tests
{
    [TestClass]
    public class NoticeTests
    {
        private const string Header =
            "カブ隊保護者のみなさま\r\n副長の〇〇です。\r\n\r\n次回の活動についてご連絡致します。";

        private static List<string> Lines(string text)
        {
            return text.Replace("\r\n", "\n").Split('\n').ToList();
        }

        private static ParseResult ParseSample()
        {
            return NewsletterParser.Parse(Lines(SampleText.Page2), Header, new DateTime(2026, 10, 4));
        }

        // ---- 解析(データ追加フェーズ) ----

        [TestMethod]
        public void Parse_詳細のある予定だけを取り込む()
        {
            ParseResult result = ParseSample();

            Assert.AreEqual(new DateTime(2026, 9, 27), result.IssueDate);
            Assert.AreEqual(3, result.Events.Count);
            Assert.AreEqual(3, result.SkippedLines.Count, "今後の予定(☆1行だけ)はスキップされる");
        }

        [TestMethod]
        public void Parse_各項目に分解される()
        {
            CubEvent e = ParseSample().Events[0];

            Assert.AreEqual(Header, e.Header);
            Assert.AreEqual(new DateTime(2026, 10, 4), e.Date);
            Assert.AreEqual("日", e.DayOfWeekText);
            Assert.AreEqual("隊集会 赤い羽根共同募金＆カントリー大作戦", e.Title);
            Assert.AreEqual("～社会・福祉のために募金活動に協力！みんなの街もきれいにしてみる？～", e.Subtitle);
            Assert.AreEqual("13時00分  Ａ駅南口藤棚下", e.Gathering);
            Assert.AreEqual("15時00分  Ａ駅南口藤棚下（予定）", e.Dismissal);
            Assert.AreEqual("Ａ駅周辺", e.Place);
            Assert.AreEqual("常備品", e.Belongings);
            Assert.AreEqual(
                "ビーバー隊とＡ駅で赤い羽根共同募金をおこないます。" + Environment.NewLine +
                "ゴミ拾いもしますので常備品となっている軍手をお忘れなく。再度確認をお願いします。",
                e.Notes);
        }

        [TestMethod]
        public void Parse_時刻が無い解散や数字入りもちものもそのまま残る()
        {
            CubEvent e = ParseSample().Events[1];

            Assert.AreEqual("【くまスカウトのみ】くま集会 ボーイ隊合同くまキャンプ", e.Title);
            Assert.AreEqual("9時00分  Ａ駅南口藤棚下", e.Gathering);
            Assert.AreEqual("翌日のカブ隊活動に合流し解散", e.Dismissal);
            Assert.AreEqual("常備品、カブ弁、1泊2日の宿泊で必要だと思うもの", e.Belongings);
        }

        [TestMethod]
        public void Parse_備考の折り返しは文の途中なら連結し文末なら改行する()
        {
            CubEvent e = ParseSample().Events[2];

            Assert.AreEqual("【全員参加】隊集会 10団と合同 ハロウィンパーティー", e.Title);
            string[] notes = e.Notes.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            CollectionAssert.AreEqual(new[]
            {
                "くまスカウトは前日のくまキャンプから合流しますので16時にお迎えをお願いします。",
                "現地集合解散となりますので送迎をお願いします。",
                "指導者の送迎が必要な場合にはご相談ください。",
                "野外料理を予定しており、保護者のお手伝いが必要となりますので改めて調整さんでスカウト出欠とお手伝い可否について確認しますのでよろしくお願いします。",
            }, notes);
        }

        [TestMethod]
        public void Parse_全角数字や空白が無い書き方でも読める()
        {
            string text = "☆　１０月１８日（日）　隊集会\n集　合：９時００分　炊事場\n解　散：１６時００分　炊事場";
            CubEvent e = NewsletterParser.Parse(Lines(text), "", new DateTime(2026, 10, 1)).Events.Single();

            Assert.AreEqual(new DateTime(2026, 10, 18), e.Date);
            Assert.AreEqual("隊集会", e.Title);
            Assert.AreEqual("9時00分  炊事場", e.Gathering);
            Assert.AreEqual("16時00分  炊事場", e.Dismissal);
            Assert.AreEqual("", e.Subtitle);
            Assert.AreEqual("", e.Notes);
        }

        [TestMethod]
        public void InferDate_年をまたぐ予定は翌年になる()
        {
            DateTime issue = new DateTime(2026, 9, 27);
            Assert.AreEqual(new DateTime(2026, 10, 4), NewsletterParser.InferDate(10, 4, issue));
            Assert.AreEqual(new DateTime(2027, 1, 17), NewsletterParser.InferDate(1, 17, issue));
            Assert.AreEqual(new DateTime(2026, 9, 1), NewsletterParser.InferDate(9, 1, issue));
        }

        // ---- 整形(データ取得フェーズ) ----

        [TestMethod]
        public void Format_整形後の例と同じ形になる()
        {
            CubEvent e = ParseSample().Events[0];
            string expected = string.Join(Environment.NewLine, new[]
            {
                "カブ隊保護者のみなさま",
                "副長の〇〇です。",
                "",
                "次回の活動についてご連絡致します。",
                "",
                "10月 4日(日) 隊集会 赤い羽根共同募金＆カントリー大作戦",
                "～社会・福祉のために募金活動に協力！みんなの街もきれいにしてみる？～",
                "集 合 ：  13時00分  Ａ駅南口藤棚下",
                "解 散 ：  15時00分  Ａ駅南口藤棚下（予定）",
                "",
                "活動場所： Ａ駅周辺",
                "もちもの ： 常備品",
                "",
                "ビーバー隊とＡ駅で赤い羽根共同募金をおこないます。",
                "ゴミ拾いもしますので常備品となっている軍手をお忘れなく。再度確認をお願いします。",
            });

            Assert.AreEqual(expected, AnnouncementFormatter.Format(e, AnnouncementFormatter.DefaultTemplate));
        }

        [TestMethod]
        public void Format_サブタイトルと備考が無ければその行は消える()
        {
            CubEvent e = new CubEvent
            {
                Header = "ヘッダ",
                Date = new DateTime(2026, 11, 3),
                DayOfWeekText = "火・祝",
                Title = "県央地区ラリー",
                Gathering = "9時00分  集合場所",
                Dismissal = "15時00分  集合場所",
                Place = "公園",
                Belongings = "常備品",
            };
            string actual = AnnouncementFormatter.Format(e, AnnouncementFormatter.DefaultTemplate);

            StringAssert.StartsWith(actual, "ヘッダ" + Environment.NewLine + Environment.NewLine + "11月 3日(火・祝) 県央地区ラリー" + Environment.NewLine + "集 合 ：  9時00分  集合場所");
            StringAssert.EndsWith(actual, "もちもの ： 常備品");
        }

        [TestMethod]
        public void DateText_曜日が無ければ日付から求める()
        {
            CubEvent e = new CubEvent { Date = new DateTime(2026, 10, 17) };
            Assert.AreEqual("10月17日(土)", e.DateText);
        }

        // ---- 保存・取得・破棄 ----

        [TestMethod]
        public void Store_次回取得と過去データ削除()
        {
            string path = Path.Combine(Path.GetTempPath(), "CubNoticeTest_" + Guid.NewGuid().ToString("N"), "events.json");
            try
            {
                EventStore store = new EventStore(path);
                store.AddOrUpdate(ParseSample().Events);
                store.Save();

                EventStore reloaded = new EventStore(path);
                reloaded.Load();
                Assert.AreEqual(3, reloaded.Events.Count);

                // 当日の予定は「次回」に含める
                Assert.AreEqual(new DateTime(2026, 10, 4), reloaded.FindNext(new DateTime(2026, 10, 4)).Date);

                // 10/5に問い合わせると10/4は破棄され、次回は10/17
                DateTime today = new DateTime(2026, 10, 5);
                List<CubEvent> removed = reloaded.RemovePast(today);
                Assert.AreEqual(1, removed.Count);
                Assert.AreEqual(new DateTime(2026, 10, 17), reloaded.FindNext(today).Date);

                // すべて過ぎたら次回は無し
                reloaded.RemovePast(new DateTime(2026, 10, 19));
                Assert.IsNull(reloaded.FindNext(new DateTime(2026, 10, 19)));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(path), true);
            }
        }

        [TestMethod]
        public void Store_同じ日付とタイトルは上書きされる()
        {
            EventStore store = new EventStore(Path.Combine(Path.GetTempPath(), "unused.json"));
            Tuple<int, int> first = store.AddOrUpdate(ParseSample().Events);
            Tuple<int, int> second = store.AddOrUpdate(ParseSample().Events);

            Assert.AreEqual(Tuple.Create(3, 0), first);
            Assert.AreEqual(Tuple.Create(0, 3), second);
            Assert.AreEqual(3, store.Events.Count);
        }
    }
}
