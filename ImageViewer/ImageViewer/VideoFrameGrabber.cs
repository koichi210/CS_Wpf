using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ImageViewer
{
    // 動画の指定した位置のコマを画像(BitmapSource)として取り出す
    // (WinForms版・WPF版のImageViewerで同じ内容のファイルを使っている。WinForms版もWPFのMediaPlayerを使う)。
    //  ・PATHにffmpeg.exeがあれば、それで1コマだけPNGに書き出して読む(コマ番号の指定も正確)。
    //  ・無ければWPFのMediaPlayerで動画を開き、その位置へ移動して描いた絵を写し取る
    //    (追加のdllは不要。Windowsに入っているコーデックで再生できる動画だけ。コマ番号は fps から時間に直して近いコマになる)。
    // 結果は呼び出したスレッド(UIスレッド)で completed(画像, エラーの文言) として返す。成功なら文言はnull
    public static class VideoFrameGrabber
    {
        // MediaPlayerは位置を変えてから絵が描けるまでの「描けた」イベントが無いので、少し待ってから写す
        private static readonly TimeSpan _renderWait = TimeSpan.FromMilliseconds(600);
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

        // PATHからffmpeg.exeを探す。見つからなければnull
        public static string FindFfmpeg()
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    string trimmed = dir.Trim().Trim('"');
                    if (trimmed.Length == 0)
                    {
                        continue;
                    }
                    string candidate = Path.Combine(trimmed, "ffmpeg.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                    // PATHにパスとして使えない文字が入っている欄は飛ばす
                }
            }
            return null;
        }

        public static void Grab(string videoPath, VideoPosition position, Action<BitmapSource, string> completed)
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            string ffmpeg = FindFfmpeg();
            if (ffmpeg != null)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    string error;
                    BitmapSource image = GrabWithFfmpeg(ffmpeg, videoPath, position, out error);
                    dispatcher.BeginInvoke(new Action(() => completed(image, error)));
                });
                return;
            }

            GrabWithMediaPlayer(videoPath, position.Time, completed);
        }

        private static BitmapSource GrabWithFfmpeg(string ffmpeg, string videoPath, VideoPosition position, out string error)
        {
            error = null;
            string outputPath = Path.Combine(Path.GetTempPath(), "ImageViewer_frame_" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(ffmpeg, VideoPosition.BuildFfmpegArguments(videoPath, position, outputPath))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                };
                using (Process process = Process.Start(info))
                {
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (!File.Exists(outputPath))
                    {
                        error = "ffmpegでコマを取り出せませんでした(動画の長さを超えた位置かもしれません)" +
                            (stderr.Trim().Length != 0 ? Environment.NewLine + stderr.Trim() : "");
                        return null;
                    }
                }

                BitmapImage image = new BitmapImage();
                using (FileStream stream = File.OpenRead(outputPath))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                }
                image.Freeze();
                return image;
            }
            catch (Exception ex)
            {
                error = "ffmpegの実行に失敗しました: " + ex.Message;
                return null;
            }
            finally
            {
                try
                {
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }
                }
                catch (IOException)
                {
                    // 一時ファイルが消せなくても表示には影響しない
                }
            }
        }

        private static void GrabWithMediaPlayer(string videoPath, TimeSpan position, Action<BitmapSource, string> completed)
        {
            MediaPlayer player = new MediaPlayer { ScrubbingEnabled = true, IsMuted = true };
            DispatcherTimer timeoutTimer = new DispatcherTimer { Interval = _timeout };
            DispatcherTimer renderTimer = new DispatcherTimer { Interval = _renderWait };
            bool isFinished = false;

            Action<BitmapSource, string> finish = (image, error) =>
            {
                if (isFinished)
                {
                    return;
                }
                isFinished = true;
                timeoutTimer.Stop();
                renderTimer.Stop();
                player.Close();
                completed(image, error);
            };

            player.MediaFailed += (s, e) =>
                finish(null, "動画を開けませんでした(このPCに再生できるコーデックが無いかもしれません): "
                    + (e.ErrorException != null ? e.ErrorException.Message : ""));

            player.MediaOpened += (s, e) =>
            {
                if (player.NaturalVideoWidth <= 0 || player.NaturalVideoHeight <= 0)
                {
                    finish(null, "映像のトラックがありません");
                    return;
                }
                if (player.NaturalDuration.HasTimeSpan && position > player.NaturalDuration.TimeSpan)
                {
                    finish(null, "動画の長さ(" + player.NaturalDuration.TimeSpan.TotalSeconds.ToString("0.###") + "秒)を超えた位置です");
                    return;
                }

                player.Pause();
                player.Position = position;
                renderTimer.Start();
            };

            renderTimer.Tick += (s, e) =>
            {
                renderTimer.Stop();
                try
                {
                    int width = player.NaturalVideoWidth;
                    int height = player.NaturalVideoHeight;
                    DrawingVisual visual = new DrawingVisual();
                    using (DrawingContext context = visual.RenderOpen())
                    {
                        context.DrawVideo(player, new Rect(0, 0, width, height));
                    }
                    RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(visual);
                    bitmap.Freeze();
                    finish(bitmap, null);
                }
                catch (Exception ex)
                {
                    finish(null, "コマを画像にできませんでした: " + ex.Message);
                }
            };

            timeoutTimer.Tick += (s, e) => finish(null, "動画の読み込みが時間内に終わりませんでした");

            try
            {
                timeoutTimer.Start();
                player.Open(new Uri(Path.GetFullPath(videoPath)));
                // Openだけでは読み込みが始まらない環境があるので、一時停止の状態にしておく
                player.Pause();
            }
            catch (Exception ex)
            {
                finish(null, "動画を開けませんでした: " + ex.Message);
            }
        }
    }
}
