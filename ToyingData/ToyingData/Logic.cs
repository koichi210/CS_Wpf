using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;

namespace ToyingData
{
    /// <summary>
    /// 全角→半角変換のロジック。WinForms版(CS_Form\ToyingData\Logic.cs)をそのまま流用している。
    ///
    /// TryGetRegexPattern(旧 GetRegesStr) は4つのチェックボックスの状態を直接参照していたのを、bool引数に
    /// 置き換えたもの。MessageBox を出す判断（対象が1つも選ばれていない）は画面側に残し、
    /// ここには含めていない。
    /// </summary>
    internal static class Logic
    {
        /// <summary>選ばれた変換対象から、正規表現の文字クラスを組み立てる。1つも選ばれていなければfalse。</summary>
        public static bool TryGetRegexPattern(bool number, bool alphaLarge, bool alphaSmall, bool space, out string regexPattern)
        {
            string charClass = (number ? "０-９" : "")
                + (alphaLarge ? "Ａ-Ｚ" : "")
                + (alphaSmall ? "ａ-ｚ" : "")
                + (space ? "　" : "");
            regexPattern = "[" + charClass + "]";

            return charClass.Length > 0;
        }

        /// <summary>指定した正規表現の文字クラスに一致する文字を、全角→半角へ変換する。</summary>
        public static string[] ApplyWide2Narrow(string[] lines, string regexPattern)
        {
            Regex re = new Regex(regexPattern);
            return lines.Select(str => re.Replace(str, ToNarrow)).ToArray();
        }

        private static string ToNarrow(Match m)
        {
            // Memo: 参照設定に「Microsoft.VisualBasic」が必要
            return Strings.StrConv(m.Value, VbStrConv.Narrow);
        }
    }
}
