using System;
using System.Globalization;

namespace ImageViewer
{
    // 動画のどのコマを表示するか(時間かコマ番号)。入力欄の文字列の読み取りと、
    // ffmpegに渡す引数の組み立てをする。画面に依存しないのでテストできる
    // (WinForms版・WPF版のImageViewerで同じ内容のファイルを使っている)
    public sealed class VideoPosition
    {
        public const double DefaultFps = 30.0;

        // trueならコマ番号で指定した(FrameNumberが1始まりのコマ番号)。falseなら時間で指定した
        public bool IsFrame { get; private set; }

        // 1始まりのコマ番号(1が先頭のコマ)。時間で指定したときは0
        public long FrameNumber { get; private set; }

        // コマ番号から時間を出すときの1秒あたりのコマ数
        public double Fps { get; private set; }

        // 表示する位置(コマ番号で指定したときは (コマ番号 - 1) / fps)
        public TimeSpan Time { get; private set; }

        public static VideoPosition FromTime(TimeSpan time)
        {
            return new VideoPosition { IsFrame = false, Time = time < TimeSpan.Zero ? TimeSpan.Zero : time, Fps = DefaultFps };
        }

        public static VideoPosition FromFrame(long frameNumber, double fps)
        {
            long frame = Math.Max(1, frameNumber);
            double usedFps = (fps > 0 && !double.IsInfinity(fps)) ? fps : DefaultFps;
            return new VideoPosition
            {
                IsFrame = true,
                FrameNumber = frame,
                Fps = usedFps,
                Time = TimeSpan.FromTicks((long)Math.Round((frame - 1) / usedFps * TimeSpan.TicksPerSecond)),
            };
        }

        // 時間の書き方: 秒("12.5")、分:秒("1:23.5")、時:分:秒("1:02:03")。空欄は0秒
        public static bool TryParseTime(string text, out TimeSpan time)
        {
            time = TimeSpan.Zero;
            string trimmed = (text ?? "").Trim();
            if (trimmed.Length == 0)
            {
                return true;
            }

            string[] parts = trimmed.Split(':');
            if (parts.Length > 3)
            {
                return false;
            }

            double seconds = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                double value;
                bool isLast = i == parts.Length - 1;
                if (!double.TryParse(parts[i].Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value))
                {
                    return false;
                }
                // 秒以外(時・分)は整数、分・秒の欄は60未満(先頭の欄だけは上限なし)
                if ((!isLast && value != Math.Floor(value)) || (i > 0 && value >= 60))
                {
                    return false;
                }
                seconds = seconds * 60 + value;
            }

            time = TimeSpan.FromTicks((long)Math.Round(seconds * TimeSpan.TicksPerSecond));
            return true;
        }

        // 位置の欄・単位(コマか)・fpsの欄から位置を作る。読めなければfalseとエラーの文言を返す
        public static bool TryParse(string positionText, bool isFrame, string fpsText, out VideoPosition position, out string error)
        {
            position = null;
            error = null;

            if (!isFrame)
            {
                TimeSpan time;
                if (!TryParseTime(positionText, out time))
                {
                    error = "位置は秒(例: 12.5)か 分:秒(例: 1:23.5)で入力してください";
                    return false;
                }
                position = FromTime(time);
                return true;
            }

            long frame;
            string frameText = (positionText ?? "").Trim();
            if (frameText.Length == 0)
            {
                frame = 1;
            }
            else if (!long.TryParse(frameText, NumberStyles.None, CultureInfo.InvariantCulture, out frame) || frame < 1)
            {
                error = "コマ番号は1以上の整数で入力してください(1が先頭のコマ)";
                return false;
            }

            double fps;
            string trimmedFps = (fpsText ?? "").Trim();
            if (trimmedFps.Length == 0)
            {
                fps = DefaultFps;
            }
            else if (!double.TryParse(trimmedFps, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out fps) || fps <= 0)
            {
                error = "fpsは0より大きい数で入力してください(例: 29.97)";
                return false;
            }

            position = FromFrame(frame, fps);
            return true;
        }

        // 保存するファイル名に付ける位置の文字列(例: "12.500s"、"f120")
        public string ToFileNameSuffix()
        {
            return IsFrame
                ? "f" + FrameNumber.ToString(CultureInfo.InvariantCulture)
                : Time.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture) + "s";
        }

        // ffmpegで1コマだけをPNGに書き出すときの引数。
        // 時間指定は -ss で探し(速い)、コマ番号指定は select フィルターで n 番目(0始まり)のコマを正確に取り出す
        public static string BuildFfmpegArguments(string inputPath, VideoPosition position, string outputPath)
        {
            string common = "-hide_banner -loglevel error -nostdin ";
            if (position.IsFrame)
            {
                return common + "-i \"" + inputPath + "\" -vf \"select=eq(n\\," + (position.FrameNumber - 1).ToString(CultureInfo.InvariantCulture)
                    + ")\" -vsync 0 -frames:v 1 -y \"" + outputPath + "\"";
            }
            return common + "-ss " + position.Time.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture)
                + " -i \"" + inputPath + "\" -frames:v 1 -y \"" + outputPath + "\"";
        }
    }
}
