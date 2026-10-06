using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CubNotice
{
    /// <summary>解析結果。</summary>
    public class ParseResult
    {
        public List<CubEvent> Events { get; } = new List<CubEvent>();

        /// <summary>「2026/9/27 発行」から読み取った発行日(無ければnull)。</summary>
        public DateTime? IssueDate { get; set; }

        /// <summary>集合・解散などの詳細が無く、日付とタイトルだけ取り込んだ予定(今後の予定など)の件数。</summary>
        public int SummaryCount
        {
            get { return Events.Count(e => !e.HasDetail); }
        }
    }

    /// <summary>
    /// カブ8通信の予定ページのテキストを、活動予定(CubEvent)の一覧に変換する。
    /// </summary>
    public static class NewsletterParser
    {
        private static readonly Regex IssueDateRegex =
            new Regex(@"(\d{4})\s*/\s*(\d{1,2})\s*/\s*(\d{1,2})\s*発行");

        private static readonly Regex EventHeadRegex =
            new Regex(@"^[☆★]\s*(\d{1,2})\s*月\s*(\d{1,2})\s*日\s*[(（]\s*([^)）]*?)\s*[)）]\s*(.*)$");

        private static readonly Regex FieldRegex =
            new Regex(@"^(集\s*合|解\s*散|活動場所|もちもの|持ち物)\s*[:：]\s*(.*)$");

        private static readonly Regex TimeAndPlaceRegex =
            new Regex(@"^(\d{1,2}時\d{1,2}分)\s*(.*)$");

        private enum Part { Title, Subtitle, Field, Note }

        /// <summary>
        /// テキスト行を解析する。
        /// </summary>
        /// <param name="lines">ページのテキスト行</param>
        /// <param name="header">各予定に設定する固定ヘッダ</param>
        /// <param name="today">発行日が読めなかった時に年を推定する基準日</param>
        public static ParseResult Parse(IEnumerable<string> lines, string header, DateTime today)
        {
            List<string> normalized = lines.Select(Normalize).Where(l => l.Length > 0).ToList();
            ParseResult result = new ParseResult();

            foreach (string line in normalized)
            {
                Match m = IssueDateRegex.Match(line);
                if (m.Success)
                {
                    result.IssueDate = new DateTime(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
                    break;
                }
            }
            DateTime baseDate = result.IssueDate ?? today.Date;

            Block current = null;
            foreach (string line in normalized)
            {
                Match head = EventHeadRegex.Match(line);
                if (head.Success)
                {
                    Close(current, result);
                    current = new Block(new CubEvent
                    {
                        Header = header ?? "",
                        Date = InferDate(int.Parse(head.Groups[1].Value), int.Parse(head.Groups[2].Value), baseDate),
                        DayOfWeekText = head.Groups[3].Value.Trim(),
                    });
                    current.SetTitle(head.Groups[4].Value);
                    continue;
                }
                if (current == null)
                {
                    continue;
                }
                if (IsSectionBreak(line))
                {
                    Close(current, result);
                    current = null;
                    continue;
                }
                current.Add(line);
            }
            Close(current, result);
            return result;
        }

        private static void Close(Block block, ParseResult result)
        {
            if (block == null)
            {
                return;
            }
            // 詳細(集合・解散など)が無い「今後の予定」も、日付とタイトルだけで取り込む
            block.Event.Notes = string.Join(Environment.NewLine, block.Notes);
            result.Events.Add(block.Event);
        }

        private static bool IsSectionBreak(string line)
        {
            return line.StartsWith("≪") || line.StartsWith("<<") || line.StartsWith("＜＜") || line.StartsWith("《");
        }

        /// <summary>
        /// 月日から年を推定する。基準日より3か月以上前になる月日は翌年とみなす
        /// (9月発行号に載る1月の予定など)。
        /// </summary>
        internal static DateTime InferDate(int month, int day, DateTime baseDate)
        {
            DateTime candidate = new DateTime(baseDate.Year, month, day);
            if (candidate < baseDate.AddMonths(-3))
            {
                candidate = candidate.AddYears(1);
            }
            return candidate;
        }

        /// <summary>
        /// 全角数字・空白のゆれをそろえ、「13 時 00 分」のような数字まわりの空白を詰める。
        /// </summary>
        internal static string Normalize(string line)
        {
            if (line == null)
            {
                return "";
            }
            StringBuilder sb = new StringBuilder(line.Length);
            foreach (char c in line)
            {
                if (c >= '０' && c <= '９')
                {
                    sb.Append((char)('0' + (c - '０')));
                }
                else if (c == '　' || c == '\t')
                {
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(c);
                }
            }
            string s = sb.ToString().Trim();
            // 「10 団」「1 泊 2 日」→「10団」「1泊2日」(数字の後ろが全角文字なら空白を詰める)
            s = Regex.Replace(s, @"(\d)\s+(?=[^\x00-\x7F（(～〜「【＜≪※・、。])", "$1");
            // 「ので 16時」「10月 4日」→「ので16時」「10月4日」
            // (「隊集会 10団」のような語の区切りや、「13時00分 1号館」の時刻と場所の区切りは残す)
            s = Regex.Replace(s, @"(?<=[ぁ-ん月時泊第約])\s+(?=\d)", "");
            s = Regex.Replace(s, @"(?<=日)\s+(?=[(（])", "");
            return s;
        }

        /// <summary>「13時00分 ○○駅」→「13時00分  ○○駅」のように時刻と場所の間を空白2つにそろえる。</summary>
        internal static string FormatTimeAndPlace(string value)
        {
            Match m = TimeAndPlaceRegex.Match(value.Trim());
            if (!m.Success)
            {
                return value.Trim();
            }
            string place = m.Groups[2].Value.Trim();
            return place.Length == 0 ? m.Groups[1].Value : m.Groups[1].Value + "  " + place;
        }

        private class Block
        {
            private Part last = Part.Title;
            private string lastField;

            public Block(CubEvent cubEvent)
            {
                Event = cubEvent;
            }

            public CubEvent Event { get; }
            public List<string> Notes { get; } = new List<string>();

            /// <summary>
            /// タイトルを設定する。「発団50周年記念式典(…)※保護者のご参加もお願いします」のように
            /// タイトル行の途中に※があれば、そこから後ろは備考にする。
            /// </summary>
            public void SetTitle(string title)
            {
                int mark = title.IndexOf('※');
                if (mark >= 0)
                {
                    Notes.Add(title.Substring(mark + 1).Trim());
                    title = title.Substring(0, mark);
                    last = Part.Note;
                }
                Event.Title = title.Trim();
            }

            public void Add(string line)
            {
                Match field = FieldRegex.Match(line);
                if (field.Success)
                {
                    lastField = Regex.Replace(field.Groups[1].Value, @"\s", "");
                    SetField(lastField, field.Groups[2].Value.Trim());
                    last = Part.Field;
                    return;
                }
                if (line.StartsWith("※") || line.StartsWith("＊"))
                {
                    Notes.Add(line.Substring(1).Trim());
                    last = Part.Note;
                    return;
                }
                if (line.StartsWith("～") || line.StartsWith("〜"))
                {
                    Event.Subtitle = line;
                    last = Part.Subtitle;
                    return;
                }

                // 継続行(前の項目が折り返されたもの)
                switch (last)
                {
                    case Part.Note:
                        // 前の行が「。」で終わっていれば別の文として改行、途中なら連結
                        int lastIndex = Notes.Count - 1;
                        if (Notes[lastIndex].EndsWith("。"))
                        {
                            Notes.Add(line);
                        }
                        else
                        {
                            Notes[lastIndex] += line;
                        }
                        break;
                    case Part.Field:
                        SetField(lastField, GetField(lastField) + line);
                        break;
                    case Part.Subtitle:
                        Event.Subtitle += line;
                        break;
                    default:
                        Event.Title = (Event.Title + " " + line).Trim();
                        break;
                }
            }

            private void SetField(string name, string value)
            {
                switch (name)
                {
                    case "集合": Event.Gathering = FormatTimeAndPlace(value); break;
                    case "解散": Event.Dismissal = FormatTimeAndPlace(value); break;
                    case "活動場所": Event.Place = value; break;
                    default: Event.Belongings = value; break;
                }
            }

            private string GetField(string name)
            {
                switch (name)
                {
                    case "集合": return Event.Gathering;
                    case "解散": return Event.Dismissal;
                    case "活動場所": return Event.Place;
                    default: return Event.Belongings;
                }
            }
        }
    }
}
