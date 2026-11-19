using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ImageViewer
{
    // 一覧の並び順
    public enum FileSortOrder
    {
        Name = 0,
        Size = 1,
        Date = 2,
    }

    // 表示するファイルを、名前順・サイズ順・更新日時順に並べ替える。画面に依存しないのでテストできる
    // (WinForms版・WPF版のImageViewerで同じ内容のファイルを使っている)
    public static class FileSorter
    {
        // 並び順の選択肢に出す名前(FileSortOrderの値の順)
        public static readonly string[] DisplayNames = { "名前順", "サイズ順", "更新日時順" };

        // エクスプローラーと同じ「数字を数として比べる」名前の比較(img2 → img10 の順になる)
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string x, string y);

        private sealed class LogicalNameComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                return StrCmpLogicalW(Path.GetFileName(x ?? ""), Path.GetFileName(y ?? ""));
            }
        }

        // 選択肢の番号(コンボボックスのSelectedIndex)を並び順に直す。範囲外は名前順
        public static FileSortOrder FromIndex(int index)
        {
            return (index >= 0 && index < DisplayNames.Length) ? (FileSortOrder)index : FileSortOrder.Name;
        }

        // filesを並べ替えた新しい配列を返す。サイズ・日時が同じものは名前順にする。
        // getSize/getTimeはテストで差し替えられるようにしてある(省略時は実ファイルのサイズ・更新日時。
        // 読めないファイルは0/最小の日時として扱い、一覧から落とさない)
        public static string[] Sort(IEnumerable<string> files, FileSortOrder order, bool descending,
            Func<string, long> getSize = null, Func<string, DateTime> getTime = null)
        {
            List<string> list = (files ?? Enumerable.Empty<string>()).ToList();
            IComparer<string> nameComparer = new LogicalNameComparer();
            getSize = getSize ?? GetFileSize;
            getTime = getTime ?? GetLastWriteTime;

            Comparison<string> compare;
            switch (order)
            {
                case FileSortOrder.Size:
                    Dictionary<string, long> sizes = list.Distinct().ToDictionary(file => file, getSize);
                    compare = (a, b) =>
                    {
                        int result = sizes[a].CompareTo(sizes[b]);
                        return result != 0 ? result : nameComparer.Compare(a, b);
                    };
                    break;
                case FileSortOrder.Date:
                    Dictionary<string, DateTime> times = list.Distinct().ToDictionary(file => file, getTime);
                    compare = (a, b) =>
                    {
                        int result = times[a].CompareTo(times[b]);
                        return result != 0 ? result : nameComparer.Compare(a, b);
                    };
                    break;
                default:
                    compare = nameComparer.Compare;
                    break;
            }

            // List.Sortは安定ソートではないが、同じ値は名前で比べているので並びは決まる
            list.Sort(descending ? (Comparison<string>)((a, b) => compare(b, a)) : compare);
            return list.ToArray();
        }

        private static long GetFileSize(string file)
        {
            try
            {
                return new FileInfo(file).Length;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static DateTime GetLastWriteTime(string file)
        {
            try
            {
                return File.GetLastWriteTime(file);
            }
            catch (Exception)
            {
                return DateTime.MinValue;
            }
        }
    }
}
