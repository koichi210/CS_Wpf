using System;
using System.Text;
using StandardTemplate;

namespace TrimHtmlData
{
    /// <summary>
    /// HTMLソースから検索ワードにヒットする行(とそこから指定行数)を抽出するロジック。
    /// WinForms版(CS_Form\TrimHtmlData\Logic.cs)をそのまま持ってきたもの(挙動は変えていない)。
    /// 画面のコントロール参照(最初のヒットだけ等)は、呼び出し元(MainWindow)で読み取った値を引数で渡す。
    ///
    /// trimLineNum が2以上でヒット行が末尾付近の場合は、末尾でクランプする(取得できる行数が足りなければ、ある分だけ返す)。
    /// </summary>
    internal static class Logic
    {
        public static int GetTrimLine(string trimLineNumText)
        {
            StcUtils util = new StcUtils();
            int trimLineNum = util.GetInteger(trimLineNumText);
            if (trimLineNum == 0)
            {
                trimLineNum = 1;
            }

            return trimLineNum;
        }

        public static string GetSearchString(string htmlSource, string searchWord, int trimLineNum, StringComparison comparison, bool firstWordOnly)
        {
            string[] lines = htmlSource.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            // String +=はヒット行数が多いほど文字列全体のコピーが積み上がって遅くなるため、StringBuilderを使う
            StringBuilder result = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].IndexOf(searchWord, comparison) != -1)
                {
                    // ヒットした行を含む指定行数分取得(末尾を超えないようクランプする)
                    for (int j = 0; j < trimLineNum && (i + j) < lines.Length; j++)
                    {
                        result.Append(lines[i + j]).Append(Environment.NewLine);
                    }

                    // 最初に見つかったワードのみ
                    if (firstWordOnly)
                    {
                        break;
                    }

                    // 次のワードとの境界
                    result.Append(Environment.NewLine);
                }
            }

            return result.Append(Environment.NewLine).ToString();
        }
    }
}
