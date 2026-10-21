using System;
using System.Collections.Generic;
using System.Text;
using StandardTemplate;

namespace TrimFileData
{
    /// <summary>
    /// 検索ワードリストとリファレンスデータから該当行を抽出するロジック。
    /// WinForms版(CS_Form\TrimFileData\Logic.cs)をそのまま持ってきたもの(挙動は変えていない)。
    /// 画面のコントロール参照(大文字小文字の区別等)は、呼び出し元(MainWindow)で読み取った値を引数で渡す。
    /// </summary>
    internal static class Logic
    {
        // 行の区切りとして扱う改行コード(TrimHtmlDataのLogic.SplitLinesと同じ)。以前はEnvironment.NewLine(CRLF)だけで分けていたため、
        // LFだけのリファレンスファイルが丸ごと1行扱いになり、検索ワードが1つでもヒットするとファイル全体が結果に出ていた。
        // "\r\n"を先頭に置くのは、CRLFを"\r"と"\n"の2回の区切り(=間に空行)と見なさないため
        private static readonly string[] _newLines = { "\r\n", "\n", "\r" };

        // 改行コード(CRLF/LF/CR、混在も可)を問わず行に分ける。空行は捨てる
        public static string[] SplitLines(string text)
        {
            return text.Split(_newLines, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string GetSearchData(string[] searchWordLines, string[] referLines, bool ordinalCase, bool firstWordOnly, string searchCommonWord)
        {
            StringComparison comparison = ordinalCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            StcUtils util = new StcUtils();

            // String +=はループの度に文字列全体をコピーし直すため、行数が多いと遅くなる。
            // StringBuilderに溜めてから最後に1回だけToString()する。
            StringBuilder resultBuilder = new StringBuilder();
            for (int i = 0; i < searchWordLines.Length; i++)
            {
                resultBuilder.Append("◆").Append(searchWordLines[i]).Append(Environment.NewLine);

                string[] searchWords = searchWordLines[i].Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                string hitLines = GetHitLines(searchWords, referLines, comparison, firstWordOnly, searchCommonWord);
                resultBuilder.Append(util.TrimDuplication(hitLines, Environment.NewLine));
                resultBuilder.Append(Environment.NewLine).Append(Environment.NewLine);
            }

            return resultBuilder.ToString();
        }

        public static string GetHitLines(string[] searchWords, string[] referLines, StringComparison comparison, bool firstWordOnly, string searchCommonWord)
        {
            // searchCommonWordによる絞り込みはsearchWordsのどの単語(j)でも結果が変わらないため、
            // 単語ループに入る前に1回だけreferLinesを絞り込んでおく(絞り込み自体はO(k)で済む)。
            bool hasCommonWord = searchCommonWord != "";
            List<string> filteredLines = new List<string>(referLines.Length);
            foreach (string line in referLines)
            {
                if (!hasCommonWord || line.IndexOf(searchCommonWord, comparison) != -1)
                {
                    filteredLines.Add(line);
                }
            }

            StringBuilder resultBuilder = new StringBuilder();

            for (int j = 0; j < searchWords.Length; j++)
            {
                for (int k = 0; k < filteredLines.Count; k++)
                {
                    if (filteredLines[k].IndexOf(searchWords[j], comparison) != -1)
                    {
                        // ファイルから抽出
                        resultBuilder.Append(filteredLines[k]).Append(Environment.NewLine);

                        // 最初に見つかった項目のみ抽出
                        if (firstWordOnly)
                        {
                            break;
                        }
                    }
                }
            }

            return resultBuilder.ToString();
        }
    }
}
