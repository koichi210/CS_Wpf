using System;

namespace ImageViewer
{
    // ガンマ補正の計算。画面に依存しない部分だけなのでテストできる
    // (WinForms版・WPF版のImageViewerで同じ内容のファイルを使っている)。
    // 出力 = 255 × (入力 / 255) ^ (1 / ガンマ)。ガンマが1より大きいと明るく(中間の暗い所が持ち上がる)、
    // 1より小さいと暗くなる。1.0なら変化しない。真っ黒(0)と真っ白(255)は変わらない
    public static class GammaCorrection
    {
        public const double MinGamma = 0.2;
        public const double MaxGamma = 3.0;
        public const double DefaultGamma = 1.0;

        // 補正しなくてよい(1.0)か
        public static bool IsIdentity(double gamma)
        {
            return Math.Abs(gamma - DefaultGamma) < 1e-6;
        }

        // 範囲外の値は上限・下限に収める
        public static double Clamp(double gamma)
        {
            if (double.IsNaN(gamma))
            {
                return DefaultGamma;
            }
            return Math.Min(MaxGamma, Math.Max(MinGamma, gamma));
        }

        // 0〜255の各明るさを補正後の明るさに置き換える表を作る
        public static byte[] BuildTable(double gamma)
        {
            double exponent = 1.0 / Clamp(gamma);
            byte[] table = new byte[256];
            for (int i = 0; i < table.Length; i++)
            {
                double value = 255.0 * Math.Pow(i / 255.0, exponent);
                table[i] = (byte)Math.Max(0, Math.Min(255, Math.Round(value, MidpointRounding.AwayFromZero)));
            }
            return table;
        }

        // 1画素4バイト(B,G,R,A の順)の並びに表を当てる。透明度(A)は変えない
        public static void ApplyToBgra(byte[] pixels, byte[] table)
        {
            if (pixels == null || table == null)
            {
                return;
            }

            for (int i = 0; i + 3 < pixels.Length; i += 4)
            {
                pixels[i] = table[pixels[i]];
                pixels[i + 1] = table[pixels[i + 1]];
                pixels[i + 2] = table[pixels[i + 2]];
            }
        }
    }
}
