using System;
using System.Windows;
using System.Windows.Controls;

namespace TurnMemory
{
    /// <summary>
    /// 出題画面と解答画面で共通の「10×10のマス＋行(A〜J)・列(1〜10)の見出し」。
    /// MFC版は.rcに100個のコントロールを並べていたので、その座標(ダイアログ単位)をここで再現してCanvasに配置する。
    /// 解答画面は出題画面と同じ並びで、全体が7DLU下にずれているだけなので offsetY で吸収する。
    /// </summary>
    internal sealed class CellGrid
    {
        // ダイアログ単位(DLU)→WPFのピクセル。MS Pゴシック9ptのダイアログは縦横とも1DLU≒1.5px
        public const double DluToPixel = 1.5;

        // .rcの LTEXT "A"〜"J" の位置("I"だけ1DLU右にあるのも元のまま)
        private static readonly int[] _rowLabelX = { 13, 13, 13, 13, 13, 13, 13, 13, 14, 13 };
        private static readonly int[] _rowLabelY = { 37, 50, 62, 74, 86, 98, 110, 123, 134, 145 };

        // .rcの LTEXT "1"〜"10" の位置
        private static readonly int[] _colLabelX = { 29, 46, 65, 84, 101, 120, 137, 156, 174, 190 };
        private const int _colLabelY = 25;

        // マスは左上(23, 35)から 18×12 DLU 間隔で、大きさ 17×12 DLU
        private const int _cellLeft = 23;
        private const int _cellTop = 35;
        private const int _cellPitchX = 18;
        private const int _cellPitchY = 12;
        private const int _cellWidth = 17;
        private const int _cellHeight = 12;

        private readonly FrameworkElement[,] _cells = new FrameworkElement[Logic.CellMax, Logic.CellMax];
        private readonly TextBlock[] _rowLabels = new TextBlock[Logic.CellMax];
        private readonly TextBlock[] _colLabels = new TextBlock[Logic.CellMax];

        // 見出し→マスの順にCanvasへ追加する(Tabキーの移動順もこの順になる。MFC版の.rcと同じ)
        public CellGrid(Canvas canvas, int offsetY, Func<FrameworkElement> createCell)
        {
            for (int i = 0; i < Logic.CellMax; i++)
            {
                _rowLabels[i] = new TextBlock { Text = Logic.RowLabel(i) };
                Place(canvas, _rowLabels[i], _rowLabelX[i], _rowLabelY[i] + offsetY);
            }
            for (int i = 0; i < Logic.CellMax; i++)
            {
                _colLabels[i] = new TextBlock { Text = Logic.ColLabel(i) };
                Place(canvas, _colLabels[i], _colLabelX[i], _colLabelY + offsetY);
            }
            for (int row = 0; row < Logic.CellMax; row++)
            {
                for (int col = 0; col < Logic.CellMax; col++)
                {
                    FrameworkElement cell = createCell();
                    cell.Width = Dlu(_cellWidth);
                    cell.Height = Dlu(_cellHeight);
                    Place(canvas, cell, _cellLeft + _cellPitchX * col, _cellTop + _cellPitchY * row + offsetY);
                    _cells[row, col] = cell;
                }
            }
        }

        public static double Dlu(double dlu)
        {
            return dlu * DluToPixel;
        }

        public FrameworkElement Cell(int row, int col)
        {
            return _cells[row, col];
        }

        public TextBlock RowLabel(int row)
        {
            return _rowLabels[row];
        }

        public TextBlock ColLabel(int col)
        {
            return _colLabels[col];
        }

        // size×size のマスと行・列の見出しだけを表示する(MFC版ShowCellGrid)
        public void Show(int size)
        {
            for (int i = 0; i < Logic.CellMax; i++)
            {
                for (int j = 0; j < Logic.CellMax; j++)
                {
                    _cells[i, j].Visibility = (i < size && j < size) ? Visibility.Visible : Visibility.Hidden;
                }

                Visibility edge = (i < size) ? Visibility.Visible : Visibility.Hidden;
                _colLabels[i].Visibility = edge;
                _rowLabels[i].Visibility = edge;
            }
        }

        private static void Place(Canvas canvas, UIElement element, double dluX, double dluY)
        {
            Canvas.SetLeft(element, Dlu(dluX));
            Canvas.SetTop(element, Dlu(dluY));
            canvas.Children.Add(element);
        }
    }
}
