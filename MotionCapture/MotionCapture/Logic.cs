using System;
using System.Globalization;

namespace MotionCapture
{
    /// <summary>
    /// 入力欄。MFC版MotionCaptureDlg::DoDataExchangeでの並び順（＝チェックする順番）。
    /// </summary>
    internal enum InputField
    {
        None,
        SaveFileName,
        CaptRectX,
        CaptRectY,
        CaptRectWidth,
        CaptRectHeight,
        CaptBpp,
        CaptFps,
        TimeoutSec,
        SkipFrame,
        ResizeRectWidth,
        ResizeRectHeight,
    }

    /// <summary>
    /// 画面の設定値。初期値はMFC版MotionCaptureDlgのメンバー初期値と同じ。
    /// </summary>
    internal sealed class CaptureSettings
    {
        public String SaveFileName = @"c:\ScreenCapture.avi";

        public int FrameRate = 20;
        public uint TimeoutSec = 10;
        public uint SkipFrame = 0;

        public int CaptRectX = 0;
        public int CaptRectY = 0;
        public int CaptRectWidth = 1920;
        public int CaptRectHeight = 1080;
        public uint BitmapBpp = 24;         // 0,1,4,8,16,24,32 (0 is implied by the JPEG or PNG format)

        public int ResizeRectWidth = 1024;
        public int ResizeRectHeight = 768;
        public Boolean Resize = false;

        public Boolean MousePointRec = true;
    }

    /// <summary>
    /// 画面の入力欄の文字列。UIから受け取ってLogic.Exchangeに渡す。
    /// </summary>
    internal sealed class DialogInput
    {
        public String SaveFileName = String.Empty;
        public String CaptRectX = String.Empty;
        public String CaptRectY = String.Empty;
        public String CaptRectWidth = String.Empty;
        public String CaptRectHeight = String.Empty;
        public String CaptBpp = String.Empty;
        public String CaptFps = String.Empty;
        public String TimeoutSec = String.Empty;
        public String SkipFrame = String.Empty;
        public String ResizeRectWidth = String.Empty;
        public String ResizeRectHeight = String.Empty;
        public Boolean Resize = false;
        public Boolean MousePointRec = false;
    }

    /// <summary>
    /// MFC版MotionCaptureの、画面に依存しない部分。
    /// ・UpdateData(DDX_Text/DDV_MinMaxInt)と同じ読み取り・範囲チェック・エラーメッセージ
    /// ・「記録領域の確認」で表示する縮小画像の大きさ
    /// </summary>
    internal static class Logic
    {
        // 記録領域・リサイズ後の幅と高さの上限
        public const int MaxFrameLength = 16384;

        // 「記録領域の確認」で撮った画像を表示する最大の大きさ（ピクセル）
        public const int PictureBoxWidth = 700;
        public const int PictureBoxHeight = 400;

        // MFCの標準メッセージ(l.JPN\afxres.rc)
        public const String ParseIntMessage = "整数を入力してください。";
        public const String ParseUIntMessage = "正の整数を入力してください。";

        // MFCのDDX_Textは64文字のバッファにGetWindowTextするので、63文字目までしか読まない
        private const int _textBufferLength = 64;

        public static String MakeRangeMessage(long min, long max)
        {
            return String.Format(CultureInfo.InvariantCulture, "{0} から {1} までの整数を入力してください。", min, max);
        }

        /// <summary>
        /// MFCのDDX_Textが使う _sntscanf_s(text, "%d" / "%u") と同じ読み取り。
        /// 先頭の空白を飛ばし、符号(+/-)と数字を読めるだけ読む（後ろに余計な文字があっても読めた分で成功）。
        /// 数字が1つも無ければ失敗。桁あふれはUCRTと同じく、符号付きは64bitの上下限に、符号なしは全ビット1に丸める。
        /// </summary>
        public static Boolean ScanInteger(String text, Boolean isSigned, out ulong value)
        {
            value = 0;
            if (text.Length > _textBufferLength - 1)
            {
                text = text.Substring(0, _textBufferLength - 1);
            }

            int i = 0;
            while (i < text.Length && IsCSpace(text[i]))
            {
                i++;
            }

            Boolean negative = false;
            if (i < text.Length && (text[i] == '-' || text[i] == '+'))
            {
                negative = text[i] == '-';
                i++;
            }

            Boolean readDigit = false;
            Boolean overflow = false;
            ulong number = 0;
            for (; i < text.Length && text[i] >= '0' && text[i] <= '9'; i++)
            {
                readDigit = true;
                uint digit = (uint)(text[i] - '0');
                if (number < UInt64.MaxValue / 10 || (number == UInt64.MaxValue / 10 && digit <= UInt64.MaxValue % 10))
                {
                    number = number * 10 + digit;
                }
                else
                {
                    overflow = true;
                }
            }
            if (!readDigit)
            {
                return false;
            }

            unchecked
            {
                if (isSigned && (overflow || (negative ? number > (ulong)Int64.MaxValue + 1 : number > (ulong)Int64.MaxValue)))
                {
                    value = negative ? (ulong)Int64.MinValue : (ulong)Int64.MaxValue;
                }
                else if (!isSigned && overflow)
                {
                    value = UInt64.MaxValue;
                }
                else
                {
                    value = negative ? (ulong)(-(long)number) : number;
                }
            }
            return true;
        }

        // DDX_Text(int)。読めなければ「整数を入力してください。」
        public static String ParseInt(String text, out int value)
        {
            ulong raw;
            if (!ScanInteger(text, true, out raw))
            {
                value = 0;
                return ParseIntMessage;
            }
            value = unchecked((int)(uint)raw);
            return null;
        }

        // DDX_Text(UINT)。読めなければ「正の整数を入力してください。」（"%u"なので "-1" は読めて 4294967295 になる）
        public static String ParseUInt(String text, out uint value)
        {
            ulong raw;
            if (!ScanInteger(text, false, out raw))
            {
                value = 0;
                return ParseUIntMessage;
            }
            value = unchecked((uint)raw);
            return null;
        }

        // DDV_MinMaxInt
        public static String CheckRange(int value, int min, int max)
        {
            return (value < min || value > max) ? MakeRangeMessage(min, max) : null;
        }

        /// <summary>
        /// MFC版のUpdateData(TRUE)と同じ順番で入力欄を読み、settingsへ書き込む。
        /// 最初に見つかったエラーで止め、そのときの入力欄とメッセージを返す
        /// （MFCと同じく、エラーより前の欄はsettingsへ書き込み済みになる）。
        /// </summary>
        public static Boolean Exchange(DialogInput input, CaptureSettings settings, out InputField failedField, out String message)
        {
            failedField = InputField.None;
            message = null;
            int i;
            uint u;

            settings.SaveFileName = input.SaveFileName;

            if (Fail(ParseInt(input.CaptRectX, out i), InputField.CaptRectX, ref failedField, ref message)) return false;
            settings.CaptRectX = i;
            if (Fail(ParseInt(input.CaptRectY, out i), InputField.CaptRectY, ref failedField, ref message)) return false;
            settings.CaptRectY = i;

            if (Fail(ParseInt(input.CaptRectWidth, out i), InputField.CaptRectWidth, ref failedField, ref message)) return false;
            settings.CaptRectWidth = i;
            if (Fail(CheckRange(i, 1, MaxFrameLength), InputField.CaptRectWidth, ref failedField, ref message)) return false;

            if (Fail(ParseInt(input.CaptRectHeight, out i), InputField.CaptRectHeight, ref failedField, ref message)) return false;
            settings.CaptRectHeight = i;
            if (Fail(CheckRange(i, 1, MaxFrameLength), InputField.CaptRectHeight, ref failedField, ref message)) return false;

            // MFC版はUINTの値をDDV_MinMaxIntでintとして比べている（大きすぎる値は負数になって範囲外）
            if (Fail(ParseUInt(input.CaptBpp, out u), InputField.CaptBpp, ref failedField, ref message)) return false;
            settings.BitmapBpp = u;
            if (Fail(CheckRange(unchecked((int)u), 0, 32), InputField.CaptBpp, ref failedField, ref message)) return false;

            if (Fail(ParseInt(input.CaptFps, out i), InputField.CaptFps, ref failedField, ref message)) return false;
            settings.FrameRate = i;
            if (Fail(CheckRange(i, 1, 120), InputField.CaptFps, ref failedField, ref message)) return false;

            if (Fail(ParseUInt(input.TimeoutSec, out u), InputField.TimeoutSec, ref failedField, ref message)) return false;
            settings.TimeoutSec = u;
            if (Fail(ParseUInt(input.SkipFrame, out u), InputField.SkipFrame, ref failedField, ref message)) return false;
            settings.SkipFrame = u;

            if (Fail(ParseInt(input.ResizeRectWidth, out i), InputField.ResizeRectWidth, ref failedField, ref message)) return false;
            settings.ResizeRectWidth = i;
            if (Fail(CheckRange(i, 1, MaxFrameLength), InputField.ResizeRectWidth, ref failedField, ref message)) return false;

            if (Fail(ParseInt(input.ResizeRectHeight, out i), InputField.ResizeRectHeight, ref failedField, ref message)) return false;
            settings.ResizeRectHeight = i;
            if (Fail(CheckRange(i, 1, MaxFrameLength), InputField.ResizeRectHeight, ref failedField, ref message)) return false;

            settings.Resize = input.Resize;
            settings.MousePointRec = input.MousePointRec;
            return true;
        }

        // UpdateData(FALSE)で入力欄に出す文字列（"%d" / "%u"）
        public static String Format(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        public static String Format(uint value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 「記録領域の確認」で表示する画像の大きさ。縦横比を保ったまま700×400の枠に収まるよう縮める（拡大はしない）。
        /// </summary>
        public static void CalcSampleSize(int srcWidth, int srcHeight, out int width, out int height)
        {
            double scale = Math.Min(1.0, Math.Min((double)PictureBoxWidth / srcWidth, (double)PictureBoxHeight / srcHeight));
            width = Math.Max(1, (int)(srcWidth * scale));
            height = Math.Max(1, (int)(srcHeight * scale));
        }

        // 「記録領域の確認」ダイアログのタイトル
        public static String MakeSampleCaption(int x, int y, int width, int height)
        {
            return String.Format(CultureInfo.InvariantCulture, "キャプチャ領域 ({0}, {1}) {2} x {3}", x, y, width, height);
        }

        private static Boolean Fail(String error, InputField field, ref InputField failedField, ref String message)
        {
            if (error == null)
            {
                return false;
            }
            failedField = field;
            message = error;
            return true;
        }

        // C言語のisspace（"C"ロケール）
        private static Boolean IsCSpace(char c)
        {
            return c == ' ' || c == '\t' || c == '\n' || c == '\v' || c == '\f' || c == '\r';
        }
    }
}
