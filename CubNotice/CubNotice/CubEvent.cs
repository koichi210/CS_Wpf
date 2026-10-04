using System;

namespace CubNotice
{
    /// <summary>
    /// 1回分の活動予定(カブ8通信の「☆」1ブロック)。
    /// </summary>
    public class CubEvent
    {
        /// <summary>①開催案内の固定ヘッダ</summary>
        public string Header { get; set; } = "";

        /// <summary>②開催日</summary>
        public DateTime Date { get; set; }

        /// <summary>②曜日表記(例: "日"、"火・祝")。PDFの表記をそのまま保持する</summary>
        public string DayOfWeekText { get; set; } = "";

        /// <summary>③開催タイトル</summary>
        public string Title { get; set; } = "";

        /// <summary>④開催サブタイトル(「～」で囲まれた行)</summary>
        public string Subtitle { get; set; } = "";

        /// <summary>⑤集合</summary>
        public string Gathering { get; set; } = "";

        /// <summary>⑥解散</summary>
        public string Dismissal { get; set; } = "";

        /// <summary>⑦活動場所</summary>
        public string Place { get; set; } = "";

        /// <summary>⑧もちもの</summary>
        public string Belongings { get; set; } = "";

        /// <summary>⑨備考(「※」を除いた本文。複数行あり。無い場合は空)</summary>
        public string Notes { get; set; } = "";

        /// <summary>
        /// 日付の表示形式(例: "10月 4日(日)")。日は2桁幅で右寄せする。
        /// </summary>
        public string DateText
        {
            get
            {
                string week = string.IsNullOrEmpty(DayOfWeekText) ? WeekdayName(Date) : DayOfWeekText;
                return string.Format("{0}月{1,2}日({2})", Date.Month, Date.Day, week);
            }
        }

        public static string WeekdayName(DateTime date)
        {
            return "日月火水木金土".Substring((int)date.DayOfWeek, 1);
        }

        public CubEvent Clone()
        {
            return (CubEvent)MemberwiseClone();
        }
    }
}
