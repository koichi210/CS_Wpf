using System;
using System.Collections.Generic;
using System.Globalization;

namespace TurnMemory
{
    // 出題の進行状態(MFC版TurnMemoryDlgのGameStateと同じ4状態)
    internal enum GameState
    {
        Init,
        Start,
        Stop,
        End,
    }

    /// <summary>
    /// 順番記憶ゲームの出題・答え合わせのロジック。
    /// MFC版TurnMemoryDlg/AnsDlgから画面操作を取り除き、タイマの1回分の処理(ShowNext/WaitTick)を単体で呼べるようにしたもの。
    /// マスのindexは左上から行ごとの通し番号(row * Size + col)。
    /// 乱数はコンストラクタで差し替えられるようにして、テストで出題順を固定できるようにしている。
    /// </summary>
    internal sealed class Logic
    {
        public const int CellMax = 10;      // 1辺の最大マス数
        public const int CellMin = 3;       // 1辺の最小マス数
        public const int DefaultSize = 4;   // 1辺のマス数の初期値
        public const int WaitMax = 5;       // 全部表示し終えてから解答に移るまでの秒数

        // 画面に出す文言(MFC版の定数・.rcの文言そのまま)
        public const string KeepMindStr = "順番を記憶して下さい。";
        public const string RememberSecStr = "秒間覚えて下さい。";
        public const string StartStr = "スタートを押して下さい。";
        public const string EndStr = "解答を入力して下さい。";
        public const string StartButton = "スタート";
        public const string StopButton = "ストップ";
        public const string AllCorrectStr = "全問正解！！";
        public const string WrongStr = "残念。。";

        private readonly Random _random;
        private readonly int[] _answer = new int[CellMax * CellMax];   // 各マスの正解の順番
        private readonly int[] _input = new int[CellMax * CellMax];    // 各マスに入力された順番
        private int _size = DefaultSize;

        public Logic(Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }
            _random = random;
        }

        // 1辺のマス数。画面のコンボボックスの選択(3×3〜10×10)から設定する
        public int Size
        {
            get { return _size; }
            set
            {
                if (value < CellMin || value > CellMax)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                _size = value;
            }
        }

        public int CellCount
        {
            get { return _size * _size; }
        }

        public GameState State { get; private set; } = GameState.Init;

        // 次に表示する順番
        public int NextNumber { get; private set; } = 1;

        // 記憶時間の残り秒数
        public int Wait { get; private set; }

        // マス数コンボボックスの選択肢("3 × 3"〜"10 × 10")
        public static IReadOnlyList<string> SizeChoices()
        {
            List<string> choices = new List<string>();
            for (int i = CellMin; i <= CellMax; i++)
            {
                choices.Add(String.Format(CultureInfo.InvariantCulture, "{0} × {0}", i));
            }
            return choices;
        }

        public static int SizeToIndex(int size)
        {
            return size - CellMin;
        }

        public static int IndexToSize(int index)
        {
            return index + CellMin;
        }

        // 行の見出し("A"〜"J")と列の見出し("1"〜"10")
        public static string RowLabel(int row)
        {
            return ((char)('A' + row)).ToString();
        }

        public static string ColLabel(int col)
        {
            return (col + 1).ToString(CultureInfo.InvariantCulture);
        }

        // 記憶時間のカウントダウン表示("5秒間覚えて下さい。"など)
        public static string RememberText(int seconds)
        {
            return seconds.ToString(CultureInfo.InvariantCulture) + RememberSecStr;
        }

        // MFC版のGetDlgItemInt(..., FALSE)相当。空欄や数値にならない入力は0(=どの順番とも一致しない)として扱う
        public static int ParseInput(string text)
        {
            uint value;
            if (!UInt32.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value))
            {
                return 0;
            }
            return unchecked((int)value);
        }

        // 出題開始。前回の入力を消して、各マスに 1〜マス数 の順番をランダムに割り当てる
        public void Start()
        {
            Array.Clear(_input, 0, _input.Length);
            State = GameState.Start;
            NextNumber = 1;
            BuildNumber();
        }

        // 出題中に「ストップ」が押された(この後 Exit で終了状態にする。MFC版のkStopを経由する流れを踏襲)
        public void Stop()
        {
            State = GameState.Stop;
        }

        // 出題を打ち切って初期状態に戻す(正解も消すので、ストップ後は答え合わせできない)
        public void Exit()
        {
            State = GameState.End;
            NextNumber = 1;
            Array.Clear(_answer, 0, _answer.Length);
        }

        // 順番表示タイマ1回分。次の順番のマスのindexを返す(見つからなければ-1)。
        // 全部表示し終えたら finished=true になり、記憶時間のカウントダウンに移る
        public int ShowNext(out bool finished)
        {
            int index = Array.IndexOf(_answer, NextNumber, 0, CellCount);

            NextNumber++;
            finished = NextNumber > CellCount;
            if (finished)
            {
                NextNumber = 1;
                Wait = WaitMax;
            }
            return index;
        }

        // 記憶時間タイマ1回分。表示する残り秒数(5→0)を返し、0を表示した次で finished=true(解答の入力へ)
        public int WaitTick(out bool finished)
        {
            int seconds = Wait;

            Wait--;
            finished = Wait < 0;
            if (finished)
            {
                State = GameState.End;
                Wait = WaitMax;
            }
            return seconds;
        }

        public int GetAnswer(int index)
        {
            return _answer[index];
        }

        public int GetInput(int index)
        {
            return _input[index];
        }

        public void SetInput(int index, int value)
        {
            _input[index] = value;
        }

        public bool IsCorrect(int index)
        {
            return _answer[index] == _input[index];
        }

        public bool IsAllCorrect()
        {
            for (int i = 0; i < CellCount; i++)
            {
                if (!IsCorrect(i))
                {
                    return false;
                }
            }
            return true;
        }

        // 解答ダイアログの見出し
        public string JudgeTitle()
        {
            return IsAllCorrect() ? AllCorrectStr : WrongStr;
        }

        // MFC版はstd::shuffleだったが、並びは処理系依存なので同じ順にはならない(ランダムなのは同じ)。
        // 後ろから順に「自分以前のどこか」と入れ替えるFisher-Yates法
        private void BuildNumber()
        {
            Array.Clear(_answer, 0, _answer.Length);

            int count = CellCount;
            for (int i = 0; i < count; i++)
            {
                _answer[i] = i + 1;
            }
            for (int i = count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                int temp = _answer[i];
                _answer[i] = _answer[j];
                _answer[j] = temp;
            }
        }
    }
}
