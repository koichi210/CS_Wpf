using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CubNotice
{
    /// <summary>
    /// 活動予定をテンプレートに当てはめて、保護者向けのアナウンス文を作る。
    /// </summary>
    public static class AnnouncementFormatter
    {
        /// <summary>
        /// 既定のテンプレート。
        /// 「解散」は「集合」の直後に置き、その下に空行を1つ入れる。
        /// </summary>
        public const string DefaultTemplate =
            "{ヘッダ}\r\n" +
            "\r\n" +
            "{日付} {タイトル}\r\n" +
            "{サブタイトル}\r\n" +
            "集 合 ：  {集合}\r\n" +
            "解 散 ：  {解散}\r\n" +
            "\r\n" +
            "活動場所： {活動場所}\r\n" +
            "もちもの ： {もちもの}\r\n" +
            "\r\n" +
            "{備考}";

        /// <summary>テンプレートで使える差し込み項目の一覧。</summary>
        public static readonly string[] Placeholders =
        {
            "{ヘッダ}", "{日付}", "{タイトル}", "{サブタイトル}", "{集合}", "{解散}", "{活動場所}", "{もちもの}", "{備考}",
        };

        private static readonly Regex PlaceholderRegex = new Regex(@"\{[^{}\r\n]+\}");

        /// <summary>
        /// アナウンス文を作る。
        /// 差し込み項目だけの行で、その値が空なら行ごと消す(サブタイトル・備考が無い場合など)。
        /// </summary>
        public static string Format(CubEvent cubEvent, string template)
        {
            Dictionary<string, string> values = new Dictionary<string, string>
            {
                { "{ヘッダ}", cubEvent.Header },
                { "{日付}", cubEvent.DateText },
                { "{タイトル}", cubEvent.Title },
                { "{サブタイトル}", cubEvent.Subtitle },
                { "{集合}", cubEvent.Gathering },
                { "{解散}", cubEvent.Dismissal },
                { "{活動場所}", cubEvent.Place },
                { "{もちもの}", cubEvent.Belongings },
                { "{備考}", cubEvent.Notes },
            };

            string[] templateLines = (template ?? DefaultTemplate).Replace("\r\n", "\n").Split('\n');
            List<string> output = new List<string>();
            foreach (string templateLine in templateLines)
            {
                bool onlyPlaceholders = PlaceholderRegex.IsMatch(templateLine)
                    && PlaceholderRegex.Replace(templateLine, "").Trim().Length == 0;
                string line = PlaceholderRegex.Replace(templateLine, m =>
                {
                    string value;
                    return values.TryGetValue(m.Value, out value) ? Clean(value) : m.Value;
                });
                if (onlyPlaceholders && line.Trim().Length == 0)
                {
                    continue;
                }
                output.Add(line);
            }

            // 末尾の空行は落とす
            while (output.Count > 0 && output[output.Count - 1].Trim().Length == 0)
            {
                output.RemoveAt(output.Count - 1);
            }
            return string.Join(Environment.NewLine, output.Select(l => l.TrimEnd()));
        }

        private static string Clean(string value)
        {
            return (value ?? "").Replace("\r\n", "\n").Replace("\n", Environment.NewLine).Trim();
        }
    }
}
