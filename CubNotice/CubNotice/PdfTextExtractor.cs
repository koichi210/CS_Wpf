using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace CubNotice
{
    /// <summary>
    /// PDFの指定ページから、見た目の行単位でテキストを取り出す。
    /// PdfPigの文字(Letter)を基準線のY座標で行にまとめ、X座標順に並べ直す。
    /// </summary>
    public static class PdfTextExtractor
    {
        /// <summary>ページ数を返す。</summary>
        public static int GetPageCount(string pdfPath)
        {
            using (PdfDocument document = PdfDocument.Open(pdfPath))
            {
                return document.NumberOfPages;
            }
        }

        /// <summary>1始まりのページ番号でテキスト行を取得する。</summary>
        public static List<string> ExtractLines(string pdfPath, int pageNumber)
        {
            using (PdfDocument document = PdfDocument.Open(pdfPath))
            {
                if (pageNumber < 1 || pageNumber > document.NumberOfPages)
                {
                    throw new ArgumentOutOfRangeException(nameof(pageNumber),
                        string.Format("ページ{0}はありません(全{1}ページ)。", pageNumber, document.NumberOfPages));
                }
                Page page = document.GetPage(pageNumber);
                return BuildLines(page.Letters);
            }
        }

        /// <summary>文字の集まりを行テキストに組み立てる。</summary>
        internal static List<string> BuildLines(IEnumerable<Letter> letters)
        {
            List<Letter> valid = letters.Where(l => !string.IsNullOrEmpty(l.Value)).ToList();
            List<List<Letter>> rows = new List<List<Letter>>();
            List<double> rowY = new List<double>();

            // 上の行から順に処理する(PDF座標はYが大きいほど上)
            foreach (Letter letter in valid.OrderByDescending(l => l.StartBaseLine.Y))
            {
                double y = letter.StartBaseLine.Y;
                double tolerance = Math.Max(2.0, FontSize(letter) * 0.35);
                int index = rowY.FindIndex(ry => Math.Abs(ry - y) <= tolerance);
                if (index < 0)
                {
                    rows.Add(new List<Letter> { letter });
                    rowY.Add(y);
                }
                else
                {
                    rows[index].Add(letter);
                }
            }

            List<string> lines = new List<string>();
            foreach (List<Letter> row in rows)
            {
                string text = JoinRow(row.OrderBy(l => l.StartBaseLine.X).ToList());
                if (text.Trim().Length > 0)
                {
                    lines.Add(text.TrimEnd());
                }
            }
            return lines;
        }

        private static string JoinRow(List<Letter> row)
        {
            StringBuilder sb = new StringBuilder();
            Letter prev = null;
            foreach (Letter letter in row)
            {
                if (prev != null)
                {
                    double gap = letter.StartBaseLine.X - prev.EndBaseLine.X;
                    double size = Math.Max(FontSize(prev), FontSize(letter));
                    // 文字幅の半分以上空いていたら空白とみなす(元から空白文字があれば重ねない)
                    if (gap > size * 0.5 && !prev.Value.EndsWith(" ") && !letter.Value.StartsWith(" "))
                    {
                        sb.Append(gap > size * 1.5 ? "  " : " ");
                    }
                }
                sb.Append(letter.Value);
                prev = letter;
            }
            return sb.ToString();
        }

        private static double FontSize(Letter letter)
        {
            double size = letter.PointSize;
            if (size < 3)
            {
                size = letter.BoundingBox.Height;
            }
            return size <= 0 ? 10.0 : size;
        }
    }
}
