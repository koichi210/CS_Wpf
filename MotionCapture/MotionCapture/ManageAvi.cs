using System;
using System.Threading;
using System.Windows;
using static MotionCapture.NativeMethods;

namespace MotionCapture
{
    /// <summary>
    /// 録画の結果。MFC版のAviErrorと同じ並び。
    /// </summary>
    internal enum AviError
    {
        Success,
        FileOpen,
        CreateStream,
        CancelCompress,
        CreateCompressStream,
    }

    /// <summary>
    /// MFC版manage_avi.cc（画面をAVIファイルに録画する）をそのまま移したもの。
    /// VfW(avifil32.dll)をP/Invokeで呼び、MFC版と同じ形式のAVIを作る。
    /// ・フレームレート(dwRate/dwScale)と総フレーム数(録画秒数×FPS)はヘッダーに書くだけで、
    ///   実際は「1フレーム撮る→コマ送り分だけ待つ」を総フレーム数だけ繰り返す（MFC版と同じ）
    /// ・リサイズするときは記録領域をStretchBlt(COLORONCOLOR)で縮小・拡大して書き込む
    /// ・マウスポインタの描き込みもリサイズの倍率に合わせて位置をずらす
    /// </summary>
    internal sealed class ManageAvi
    {
        // 録画スレッドとUIスレッドの両方から読み書きする
        private volatile Boolean _executing = false;
        private Thread _thread;

        // 設定
        private String _saveFileName = String.Empty;
        private Boolean _compress = true;
        private Boolean _resize = false;
        private Boolean _recordMousePoint = false;
        private int _resizeWidth = 0;
        private int _resizeHeight = 0;
        private RECT _captureRect;          // 画面上の記録領域
        private int _frameWidth = 0;        // AVI の1フレームの大きさ（リサイズするときはリサイズ後の大きさ）
        private int _frameHeight = 0;
        private uint _frameRate = 0;
        private uint _scale = 1;            // 基本は等倍
        private uint _quality = unchecked((uint)-1);    // 0～10,000 (-1 はドライバーの既定値)
        private uint _bitmapBpp = 0;        // 0,1,4,8,16,24,32 (0 is implied by the JPEG or PNG format)
        private uint _timeoutSec = 0;
        private int _skipFrameMs = 0;       // フレーム間の待ち時間（ミリ秒）

        // 録画中に使う
        private AVISTREAMINFOW _aviStreamInfo;
        private BITMAPINFO _bitmapInfo;
        private IntPtr _aviFile = IntPtr.Zero;
        private IntPtr _aviStream = IntPtr.Zero;
        private IntPtr _compressAviStream = IntPtr.Zero;

        /// <summary>
        /// 録画の終わりに出すメッセージ(本文, キャプション)。既定は録画スレッドから親なしのメッセージボックスを出す
        /// （MFC版と同じく、画面を止めずに結果だけ知らせる）。テストでは差し替えて黙らせる。
        /// </summary>
        public Action<String, String> Notify { get; set; } = (text, caption) => MessageBox.Show(text, caption);

        // ---- 設定（録画前に呼び出し側が設定する） ----

        public void SetSaveFileName(String fileName)
        {
            _saveFileName = fileName;
        }

        public void SetCaptureRect(int x, int y, int width, int height)
        {
            _captureRect = new RECT { left = x, top = y, right = x + width, bottom = y + height };
        }

        public void SetFrameRate(uint frameRate)
        {
            _frameRate = frameRate;
        }

        public void SetRecordSec(uint recordSec)
        {
            _timeoutSec = recordSec;
        }

        public void SetSkipFrame(uint skipFrame)
        {
            _skipFrameMs = 1;
            if (skipFrame > 0)
            {
                _skipFrameMs = (int)(1000 / skipFrame);
            }
        }

        // テスト用
        internal int SkipFrameMs
        {
            get { return _skipFrameMs; }
        }

        public void SetCaptureQuality(uint bitmapBpp)
        {
            _bitmapBpp = bitmapBpp;
        }

        public void SetCompress(Boolean compress)
        {
            _compress = compress;
        }

        public void SetResize(Boolean resize, int resizeWidth, int resizeHeight)
        {
            _resize = resize;
            if (_resize)
            {
                _resizeWidth = resizeWidth;
                _resizeHeight = resizeHeight;
            }
        }

        public void SetRecordMousePoint(Boolean recordMousePoint)
        {
            _recordMousePoint = recordMousePoint;
        }

        // ---- 録画の開始・停止（録画は別スレッドで行う） ----

        public void StartRecord()
        {
            _executing = true;

            // VfWはCOMを使うので、MFCのワーカースレッドと同じくSTAで動かす。
            // MFC版はダイアログを閉じるとプロセスごと終わる(録画も打ち切られる)ので、バックグラウンドスレッドにする
            _thread = new Thread(RecordThreadProc);
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.IsBackground = true;
            _thread.Start();
        }

        public void StopRecord()
        {
            _executing = false;
        }

        // MFC版は「記録停止」を押した時点でfalseを返していたが、録画スレッドはまだファイルを閉じている途中で、
        // そこで次の録画を始めると前のスレッドが新しい録画のファイルを解放してしまう。
        // スレッドが終わるまでは実行中とみなし、前の録画と重ならないようにする
        public Boolean IsExecution()
        {
            Thread thread = _thread;
            return _executing || (thread != null && thread.IsAlive);
        }

        // ---- 録画処理 ----

        private void RecordThreadProc()
        {
            Boolean completed;
            AviError err = Run(out completed);

            String text;
            String caption;
            if (err == AviError.Success)
            {
                if (completed)
                {
                    // 停止されずに最後のフレームまで録画した
                    text = "タイムアウトが発生しました。\n記録を停止しファイルを保存しました。";
                    caption = "Warning";
                }
                else
                {
                    text = "ファイルを保存しました。";
                    caption = "Information";
                }
            }
            else
            {
                text = MakeErrorMessage(err, _saveFileName);
                caption = "Error";
            }
            Notify(text, caption);

            _executing = false;
        }

        public static String MakeErrorMessage(AviError err, String saveFileName)
        {
            switch (err)
            {
                case AviError.FileOpen:
                    return "AVIファイルが開けませんでした。" + saveFileName;
                case AviError.CreateStream:
                    return "Streamが生成できませんでした。";
                case AviError.CancelCompress:
                    return "圧縮設定がキャンセルされました。";
                case AviError.CreateCompressStream:
                    return "圧縮Streamが生成できませんでした。";
                default:
                    return String.Empty;
            }
        }

        /// <summary>
        /// 録画を最初から最後まで(呼んだスレッドで)行い、AVIファイルを閉じるところまで済ませる。
        /// completedは、停止されずに最後のフレームまで録画したときtrue。
        /// MFC版はメッセージボックスを閉じるまでファイルを閉じなかったが、
        /// 「保存しました」と出た時点でファイルが使えるよう、閉じてから結果を返す。
        /// </summary>
        internal AviError Run(out Boolean completed)
        {
            completed = false;
            AVIFileInit();
            try
            {
                InitAviStreamInfo();

                AviError err = CreateAviFile();
                if (err == AviError.Success)
                {
                    InitBitmapInfo();
                    err = SetStreamFormat();
                }

                if (err == AviError.Success)
                {
                    Record();
                    completed = _executing;
                }
                return err;
            }
            finally
            {
                ReleaseAvi();
                AVIFileExit();
            }
        }

        // テスト用: 別スレッドを使わず、呼んだスレッドで最後まで録画する（メッセージも出さない）
        internal AviError RecordSync(out Boolean completed)
        {
            _executing = true;
            try
            {
                return Run(out completed);
            }
            finally
            {
                _executing = false;
            }
        }

        private void InitAviStreamInfo()
        {
            _aviStreamInfo = new AVISTREAMINFOW();
            _aviStreamInfo.fccType = streamtypeVIDEO;
            _aviStreamInfo.fccHandler = comptypeDIB;
            _aviStreamInfo.dwScale = _scale;                            // dwRate / dwScale がフレームレートになる
            _aviStreamInfo.dwRate = _frameRate;
            _aviStreamInfo.dwLength = _timeoutSec * _frameRate;         // 録画時間(秒) = Length / FrameRate
            _aviStreamInfo.dwQuality = _quality;
            _aviStreamInfo.szName = String.Empty;

            // 録画する画像の大きさ。リサイズするときは縮小・拡大した後の大きさになる
            _frameWidth = _resize ? _resizeWidth : _captureRect.right - _captureRect.left;
            _frameHeight = _resize ? _resizeHeight : _captureRect.bottom - _captureRect.top;
            _aviStreamInfo.rcFrame = new RECT { left = 0, top = 0, right = _frameWidth, bottom = _frameHeight };
        }

        private void InitBitmapInfo()
        {
            _bitmapInfo = new BITMAPINFO();
            _bitmapInfo.bmiHeader.biSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            _bitmapInfo.bmiHeader.biWidth = _frameWidth;
            _bitmapInfo.bmiHeader.biHeight = _frameHeight;
            _bitmapInfo.bmiHeader.biPlanes = 1;
            _bitmapInfo.bmiHeader.biBitCount = (ushort)_bitmapBpp;
            _bitmapInfo.bmiHeader.biCompression = BI_RGB;   // BI_JPEG は指定できなかった

            // 1行は 4 バイト境界にそろえる（bpp が 0 なら 0 になる。BI_RGB なら 0 でよい）
            _bitmapInfo.bmiHeader.biSizeImage = CalcSizeImage(_frameWidth, _frameHeight, _bitmapBpp);
        }

        // MFC版と同じくUINTで計算する（大きすぎる値は桁あふれも同じになる）
        internal static uint CalcSizeImage(int width, int height, uint bpp)
        {
            unchecked
            {
                return (uint)height * (((uint)width * bpp + 31) / 32) * 4;
            }
        }

        private AviError CreateAviFile()
        {
            if (AVIERR_OK != AVIFileOpen(out _aviFile, _saveFileName, OF_CREATE | OF_WRITE, IntPtr.Zero))
            {
                return AviError.FileOpen;
            }

            if (AVIERR_OK != AVIFileCreateStream(_aviFile, out _aviStream, ref _aviStreamInfo))
            {
                return AviError.CreateStream;
            }

            return AviError.Success;
        }

        private AviError SetStreamFormat()
        {
            int formatSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(BITMAPINFO));
            _compressAviStream = IntPtr.Zero;
            if (!_compress)
            {
                AVIStreamSetFormat(_aviStream, 0, ref _bitmapInfo, formatSize);
                return AviError.Success;
            }

            COMPVARS cv = new COMPVARS();
            cv.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(COMPVARS));
            cv.dwFlags = ICMF_COMPVARS_VALID;
            cv.fccHandler = comptypeDIB;
            cv.lQ = ICQUALITY_DEFAULT;
            if (!ICCompressorChoose(IntPtr.Zero, ICMF_CHOOSE_DATARATE | ICMF_CHOOSE_KEYFRAME, ref _bitmapInfo, IntPtr.Zero, ref cv, IntPtr.Zero))
            {
                return AviError.CancelCompress;
            }

            _aviStreamInfo.fccHandler = cv.fccHandler;

            AVICOMPRESSOPTIONS opt = new AVICOMPRESSOPTIONS();
            opt.fccType = streamtypeVIDEO;
            opt.fccHandler = cv.fccHandler;
            opt.dwKeyFrameEvery = (uint)cv.lKey;
            opt.dwQuality = (uint)cv.lQ;
            opt.dwBytesPerSecond = (uint)cv.lDataRate;
            opt.dwFlags = (cv.lDataRate > 0 ? AVICOMPRESSF_DATARATE : 0)
                        | (cv.lKey > 0 ? AVICOMPRESSF_KEYFRAMES : 0);
            opt.lpFormat = IntPtr.Zero;
            opt.cbFormat = 0;
            opt.lpParms = cv.lpState;
            opt.cbParms = (uint)cv.cbState;
            opt.dwInterleaveEvery = 0;

            // opt は cv.lpState を指しているので、ストリームを作ってから解放する
            int hr = AVIMakeCompressedStream(out _compressAviStream, _aviStream, ref opt, IntPtr.Zero);
            ICCompressorFree(ref cv);
            if (AVIERR_OK != hr)
            {
                return AviError.CreateCompressStream;
            }
            AVIStreamSetFormat(_compressAviStream, 0, ref _bitmapInfo, formatSize);

            return AviError.Success;
        }

        private void Record()
        {
            IntPtr memDc = CreateCompatibleDC(IntPtr.Zero);
            IntPtr pvBits;

            // 1フレーム分の画像（大きさは _frameWidth × _frameHeight）
            IntPtr memBitmap = CreateDIBSection(IntPtr.Zero, ref _bitmapInfo, DIB_RGB_COLORS, out pvBits, IntPtr.Zero, 0);
            IntPtr oldBitmap = SelectObject(memDc, memBitmap);

            IntPtr dcScreen = CreateDC("DISPLAY", "DISPLAY", "DISPLAY", IntPtr.Zero);

            int srcLeft = _captureRect.left;
            int srcTop = _captureRect.top;
            int srcWidth = _captureRect.right - _captureRect.left;
            int srcHeight = _captureRect.bottom - _captureRect.top;
            float scaleX = (float)_frameWidth / srcWidth;
            float scaleY = (float)_frameHeight / srcHeight;
            int sizeImage = (int)_bitmapInfo.bmiHeader.biSizeImage;
            // 縮小で色が崩れないよう、間引きで縮める（HALFTONE はきれいだが毎フレームには重い）
            SetStretchBltMode(memDc, COLORONCOLOR);

            IntPtr stream = _compress ? _compressAviStream : _aviStream;

            for (uint frameNo = 0; frameNo < _aviStreamInfo.dwLength && _executing; frameNo++)
            {
                // 画面の記録領域を取り込む
                if (_resize)
                {
                    StretchBlt(memDc, 0, 0, _frameWidth, _frameHeight,
                        dcScreen, srcLeft, srcTop, srcWidth, srcHeight, SRCCOPY);
                }
                else
                {
                    BitBlt(memDc, 0, 0, _frameWidth, _frameHeight, dcScreen, srcLeft, srcTop, SRCCOPY);
                }

                if (_recordMousePoint)
                {
                    DrawCursor(memDc, srcLeft, srcTop, scaleX, scaleY);
                }

                AVIStreamWrite(stream, (int)frameNo, 1, pvBits, sizeImage, AVIIF_KEYFRAME, IntPtr.Zero, IntPtr.Zero);
                Thread.Sleep(_skipFrameMs);
            }

            SelectObject(memDc, oldBitmap);
            DeleteObject(memBitmap);
            DeleteDC(memDc);
            DeleteDC(dcScreen);
        }

        // マウスポインタを録画画像に描き込む
        // (originX, originY) は記録領域の左上（画面座標）、scaleX / scaleY はリサイズの倍率
        private static void DrawCursor(IntPtr hdc, int originX, int originY, float scaleX, float scaleY)
        {
            CURSORINFO cursorInfo = new CURSORINFO();
            cursorInfo.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(CURSORINFO));
            if (!GetCursorInfo(ref cursorInfo) || (cursorInfo.flags & CURSOR_SHOWING) == 0)
            {
                return;
            }

            ICONINFO iconInfo;
            if (!GetIconInfo(cursorInfo.hCursor, out iconInfo))
            {
                return;
            }

            int x = CalcCursorPos(cursorInfo.ptScreenPos.x, originX, scaleX, iconInfo.xHotspot);
            int y = CalcCursorPos(cursorInfo.ptScreenPos.y, originY, scaleY, iconInfo.yHotspot);
            DrawIcon(hdc, x, y, cursorInfo.hCursor);

            // GetIconInfo が作ったビットマップは呼び出し側で解放する
            if (iconInfo.hbmMask != IntPtr.Zero)
            {
                DeleteObject(iconInfo.hbmMask);
            }
            if (iconInfo.hbmColor != IntPtr.Zero)
            {
                DeleteObject(iconInfo.hbmColor);
            }
        }

        // マウスポインタを描く位置。記録領域の左上からの距離をリサイズの倍率で伸縮し、ホットスポットの分だけずらす
        internal static int CalcCursorPos(int screenPos, int origin, float scale, int hotspot)
        {
            return (int)((screenPos - origin) * scale) - hotspot;
        }

        private void ReleaseAvi()
        {
            if (_aviStream != IntPtr.Zero)
            {
                AVIStreamRelease(_aviStream);
                _aviStream = IntPtr.Zero;
            }

            if (_compressAviStream != IntPtr.Zero)
            {
                AVIStreamRelease(_compressAviStream);
                _compressAviStream = IntPtr.Zero;
            }

            if (_aviFile != IntPtr.Zero)
            {
                AVIFileRelease(_aviFile);
                _aviFile = IntPtr.Zero;
            }
        }
    }
}
