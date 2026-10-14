using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Zodiac
{
    // 下段のリストに何を並べるか(MFC版ZodiacDlgのModeと同じ)
    internal enum ViewMode
    {
        None,
        Birth,
        Age,
        Zodiac,
    }

    /// <summary>
    /// 下段のリストの1項目。MFC版はSetItemDataで年・年齢・干支の番号を持たせていたので、表示文字列と値を組で持つ。
    /// </summary>
    internal sealed class ListEntry
    {
        public ListEntry(string text, int value)
        {
            Text = text;
            Value = value;
        }

        public string Text { get; }

        public int Value { get; }

        // ComboBoxはToString()を表示する
        public override string ToString()
        {
            return Text;
        }
    }

    /// <summary>
    /// 生まれた年・年齢・干支の早見(MFC版ZodiacDlg)から、画面操作を除いた計算と文字列の組み立て。
    /// 「今年」は画面のコンボボックスで選んだ年(year)を引数で受け取る。
    /// </summary>
    internal static class Logic
    {
        public const int MinYear = 1900;
        public const int MaxYear = 2100;
        public const int MinAge = 0;
        public const int MaxAge = 130;
        public const int ZodiacCount = 12;

        public const int DefaultAge = 20;
        public const int DefaultBirth = 1980;

        public const string ProgramErrorStr = "プログラムエラー";
        public const string AllViewCaption = "早見表";

        // MinYear(1900年)が子年なので、(年 - MinYear) % 12 で引ける並び
        private static readonly string[] _zodiacNames =
        {
            "子(ねずみ)", "丑(うし)", "寅(とら)", "卯(うさぎ)", "辰(たつ)", "巳(み)",
            "午(うま)", "未(ひつじ)", "申(さる)", "酉(とり)", "戌(いぬ)", "亥(いのしし)",
        };

        // MFC版の "%14s" などの桁揃えはShift_JISのバイト数(全角2・半角1)で数えていたので、同じ幅で揃える
        private static readonly Encoding _shiftJis = Encoding.GetEncoding(932);

        public static IReadOnlyList<string> ZodiacNames { get; } = new ReadOnlyCollection<string>(_zodiacNames);

        public static string FormatAge(int age)
        {
            return String.Format(CultureInfo.InvariantCulture, "満{0}才", age);
        }

        public static string FormatBirth(int year)
        {
            return String.Format(CultureInfo.InvariantCulture, "{0}年", year);
        }

        // 「今年は○年です」の選択肢(1900〜2100)
        public static IReadOnlyList<int> YearChoices()
        {
            List<int> years = new List<int>();
            for (int year = MinYear; year <= MaxYear; year++)
            {
                years.Add(year);
            }
            return years;
        }

        // 起動時に選んでおく項目。今年が範囲外なら先頭(1900年)になるのもMFC版のまま
        public static int YearIndex(int year)
        {
            return (year >= MinYear && year <= MaxYear) ? year - MinYear : 0;
        }

        // 表示モードに合わせた下段のリストと、最初に選んでおく項目。
        // MFC版のSetCurSelは範囲外を指すと未選択になる(例: 今年を1950年にして「生まれた年」→1980年が無い)ので、そのときは-1
        public static List<ListEntry> BuildList(ViewMode mode, int year, out int selectedIndex)
        {
            List<ListEntry> list = new List<ListEntry>();
            int current;
            switch (mode)
            {
                case ViewMode.Birth:
                    for (int birth = MinYear; birth <= year; birth++)
                    {
                        list.Add(new ListEntry(FormatBirth(birth), birth));
                    }
                    current = DefaultBirth - MinYear;
                    break;

                case ViewMode.Age:
                    for (int age = MinAge; age <= MaxAge; age++)
                    {
                        list.Add(new ListEntry(FormatAge(age), age));
                    }
                    current = DefaultAge - MinAge;
                    break;

                case ViewMode.Zodiac:
                default:
                    for (int i = 0; i < ZodiacCount; i++)
                    {
                        list.Add(new ListEntry(_zodiacNames[i], i));
                    }
                    current = 0;
                    break;
            }
            selectedIndex = (current < list.Count) ? current : -1;
            return list;
        }

        // 生まれた年の干支。1900年より前や今年より後はMFC版が「プログラムエラー」を出していた範囲なので false
        public static bool TryGetZodiac(int birth, int year, out string zodiac)
        {
            if (birth < MinYear || birth > year)
            {
                zodiac = "";
                return false;
            }
            zodiac = _zodiacNames[(birth - MinYear) % ZodiacCount];
            return true;
        }

        // 「閲覧基準で表示」で右の欄に出す文字列。
        // value はリストの項目の値(生まれた年 / 年齢 / 干支の番号)。
        // 年齢から逆算した生まれ年が1900年より前だと programError=true(MFC版はメッセージを出した上で、干支を空のまま表示した)
        public static string BuildView(ViewMode mode, int value, int year, out bool programError)
        {
            programError = false;
            StringBuilder view = new StringBuilder();
            if (mode == ViewMode.Zodiac)
            {
                view.Append(_zodiacNames[value]).Append("年のヒト\r\n\r\n");
                view.Append("生まれた年    年齢\r\n");

                // 今年生まれ(0才)は含めない(MFC版の条件 birth < year のまま)
                for (int birth = MinYear + value; birth < year; birth += ZodiacCount)
                {
                    view.Append(FormatBirth(birth));
                    view.Append("        ");
                    view.Append(FormatAge(year - birth));
                    view.Append("\r\n");
                }
            }
            else
            {
                int birth = (mode == ViewMode.Age) ? year - value : value;
                int age = year - birth;
                string zodiac;
                programError = !TryGetZodiac(birth, year, out zodiac);

                view.AppendFormat(CultureInfo.InvariantCulture, "{0}年生まれ\r\n", birth);
                view.AppendFormat(CultureInfo.InvariantCulture, "今年は{0}才\r\n", age);
                view.AppendFormat(CultureInfo.InvariantCulture, "{0}年です。\r\n", zodiac);
            }
            return view.ToString();
        }

        // 「一覧表示」の早見表。1行目に干支名、以降は1900年から今年までを12年ずつ並べる
        public static string BuildAllView(int year)
        {
            StringBuilder table = new StringBuilder();
            foreach (string name in _zodiacNames)
            {
                table.Append(PadLeftByWidth(name, 14));
            }
            table.Append("\r\n");

            for (int y = MinYear; y <= year; y++)
            {
                if ((y - MinYear) % ZodiacCount == 0)
                {
                    table.Append("\r\n");
                }
                table.Append(PadLeftByWidth(y.ToString(CultureInfo.InvariantCulture), 11)).Append("年");
            }
            return table.ToString();
        }

        // printfの "%*s" をShift_JISのバイト数で数えたもの(幅を超える文字列は切らずにそのまま)
        internal static string PadLeftByWidth(string text, int width)
        {
            int padding = width - _shiftJis.GetByteCount(text);
            return (padding > 0) ? new string(' ', padding) + text : text;
        }
    }
}
