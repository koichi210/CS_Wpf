using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ImageViewer
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string _msgNotSelectDir = "フォルダが選択されていません";
        private const string _msgSelectThumbnailDir = "サムネイル表示対象のフォルダ選択";
        private const string _msgUnknownDir = "サムネイルを表示するフォルダが存在しないか、画像・動画ファイルがありません。";

        // マウスホイールでの拡大/縮小の倍率計算(1ノッチ未満の回転量を溜めておくので、インスタンスを使い回す)
        private readonly WheelZoom _wheelZoom = new WheelZoom();

        // サムネイルの読み込みの世代。開き直す・並べ替えるたびに増やし、古い読み込みは途中で打ち切る
        private int _loadGeneration;

        // 下に表示している画像の元(ガンマ補正をかける前)と、PNGで保存するときの既定のファイル名(拡張子なし)
        private BitmapSource _originalImage;
        private string _saveBaseName;

        // 動画のコマの取り出しの世代(取り出し中に別のファイルを選んだら、古い結果は捨てる)
        private int _frameGeneration;

        // ガンマのスライダーを動かしている間に毎回計算し直すと重いので、止まってから少し後に1回だけ補正する
        private readonly DispatcherTimer _gammaTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };

        public MainWindow()
        {
            InitializeComponent();

            foreach (string name in FileSorter.DisplayNames)
            {
                ComboSortOrder.Items.Add(name);
            }
            ComboSortOrder.SelectedIndex = 0;

            _gammaTimer.Tick += (s, e) =>
            {
                _gammaTimer.Stop();
                UpdateImageView();
            };
        }

        // xamlのLoadedで定義したメソッド
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            LoadSetting();
            DrawThumbnails();
        }

        // xamlのClosingで定義したメソッド
        // (デストラクタではウィンドウが消滅していて値を取得できないので、ここで保存する)
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSetting();
        }

        private void LoadSetting()
        {
            var settings = Properties.Settings.Default;
            Left = settings.ScreenLeft;
            Top = settings.ScreenTop;
            Width = settings.ScreenWidth;
            Height = settings.ScreenHeight;
            TextDirName.Text = settings.ThumbnailDirPath;
            TextFileName.Text = settings.TextFileName;
        }

        private void SaveSetting()
        {
            var settings = Properties.Settings.Default;
            // 最小化中・最大化中に閉じると、Width/Heightはタスクバー上の大きさ(160×28程度)や画面いっぱいの大きさになり、
            // 次回起動時に極小・画面サイズのウィンドウで開いてしまう。通常表示のときの位置と大きさ(RestoreBounds)を保存する
            Rect bounds = (WindowState == WindowState.Normal) ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            settings.ScreenLeft = bounds.Left;
            settings.ScreenTop = bounds.Top;
            settings.ScreenWidth = bounds.Width;
            settings.ScreenHeight = bounds.Height;
            settings.ThumbnailDirPath = TextDirName.Text;
            settings.TextFileName = TextFileName.Text;

            settings.Save();
        }

        private void ButtonOpenDialog_Click(object sender, RoutedEventArgs e)
        {
            bool isSelected = (RadioOpenTree.IsChecked == true) ? TrySelectDirectoryForForm() : TrySelectDirectory();
            if (!isSelected)
            {
                MessageBox.Show(_msgNotSelectDir);
                return;
            }
            DrawThumbnails();
        }

        // フォルダパスをコピペで指定できるUI(エクスプローラー形式)
        private bool TrySelectDirectory()
        {
            var dlg = new CommonOpenFileDialog(_msgSelectThumbnailDir)
            {
                IsFolderPicker = true
            };
            if (Directory.Exists(TextDirName.Text))
            {
                dlg.InitialDirectory = TextDirName.Text;
            }

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
            {
                return false;
            }
            TextDirName.Text = dlg.FileName;
            return true;
        }

        // フォルダパスをツリー上で掘っていくUI
        private bool TrySelectDirectoryForForm()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog()
            {
                Description = _msgSelectThumbnailDir
            };
            if (Directory.Exists(TextDirName.Text))
            {
                dlg.SelectedPath = TextDirName.Text;
            }

            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return false;
            }
            TextDirName.Text = dlg.SelectedPath;
            return true;
        }

        private void SortOrder_Changed(object sender, RoutedEventArgs e)
        {
            // 起動直後(コンストラクタで選択肢を入れた時)や、フォルダがまだ無いときは何もしない
            if (!IsLoaded || !Directory.Exists(TextDirName.Text))
            {
                return;
            }
            DrawThumbnails();
        }

        private void DrawThumbnails()
        {
            string[] files;
            if (!TryGetImageFiles(out files))
            {
                MessageBox.Show(_msgUnknownDir + Environment.NewLine + TextDirName.Text);
                return;
            }

            // 毎回新しいリストを DataContext に設定するので、既存アイテムのクリア処理は不要
            List<Thumbnail> thumbnails = files.Select(file => new Thumbnail(file)).ToList();
            DataContext = thumbnails;
            StartLoadingThumbnails(thumbnails);
        }

        private bool TryGetImageFiles(out string[] files)
        {
            files = null;
            if (!Directory.Exists(TextDirName.Text))
            {
                return false;
            }

            files = ImageFiles.GetTargetFiles(TextDirName.Text,
                FileSorter.FromIndex(ComboSortOrder.SelectedIndex), CheckSortDescending.IsChecked == true);
            return files.Length != 0;
        }

        // サムネイルを読み込み用のスレッドで1枚ずつ作り、できた順に一覧へ入れて進み具合を出す。
        // 以前はバインディングのコンバーターがUIスレッドで全部読み込んでいたため、枚数が多いと
        // 読み終わるまで画面が固まり、どこまで進んだかも分からなかった。
        // 動画のサムネイル(シェルのCOM)を作るので、スレッドはSTAにする
        private void StartLoadingThumbnails(List<Thumbnail> thumbnails)
        {
            int generation = ++_loadGeneration;
            int total = thumbnails.Count;
            ProgressLoad.Maximum = Math.Max(1, total);
            ProgressLoad.Value = 0;
            TextProgress.Text = "読み込み中 0/" + total;
            Dispatcher dispatcher = Dispatcher;

            Thread thread = new Thread(() =>
            {
                for (int i = 0; i < total; i++)
                {
                    if (generation != _loadGeneration)
                    {
                        return;
                    }

                    Thumbnail thumbnail = thumbnails[i];
                    System.Windows.Media.ImageSource image = ImageFiles.LoadThumbnail(thumbnail.FileFullPath);
                    int done = i + 1;
                    dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (generation != _loadGeneration)
                        {
                            return;
                        }
                        thumbnail.ThumbnailImage = image;
                        ProgressLoad.Value = done;
                        TextProgress.Text = (done == total) ? "読み込み完了 " + total + " 件" : "読み込み中 " + done + "/" + total;
                    }), DispatcherPriority.Background);
                }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        // 一覧で選んだファイルを下に大きく表示する。画像はそのまま、動画は指定した位置のコマを取り出して表示する
        private void ListBoxThumbnail_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            Thumbnail selected = ListBoxThumbnail.SelectedItem as Thumbnail;
            if (selected == null)
            {
                return;
            }

            if (selected.IsVideo)
            {
                ShowVideoFrame(selected.FileFullPath);
                return;
            }

            _frameGeneration++;
            BitmapSource image = ImageFiles.LoadImage(selected.FileFullPath, 0);
            TextStatus.Text = (image == null) ? "画像を読み込めませんでした: " + selected.FileName : "";
            SetOriginalImage(image, Path.GetFileNameWithoutExtension(selected.FileFullPath));
        }

        private void ButtonShowFrame_Click(object sender, RoutedEventArgs e)
        {
            Thumbnail selected = ListBoxThumbnail.SelectedItem as Thumbnail;
            if (selected == null || !selected.IsVideo)
            {
                TextStatus.Text = "一覧で動画ファイルを選んでください";
                return;
            }
            ShowVideoFrame(selected.FileFullPath);
        }

        private void TextVideoPosition_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ButtonShowFrame_Click(sender, e);
            }
        }

        private void ShowVideoFrame(string videoPath)
        {
            VideoPosition position;
            string error;
            if (!VideoPosition.TryParse(TextVideoPosition.Text, RadioVideoFrame.IsChecked == true, TextVideoFps.Text, out position, out error))
            {
                TextStatus.Text = error;
                return;
            }

            int generation = ++_frameGeneration;
            TextStatus.Text = "コマを取り出しています... " + Path.GetFileName(videoPath);
            VideoFrameGrabber.Grab(videoPath, position, (image, grabError) =>
            {
                if (generation != _frameGeneration)
                {
                    return;
                }
                if (image == null)
                {
                    TextStatus.Text = grabError;
                    return;
                }
                TextStatus.Text = Path.GetFileName(videoPath) + " の " + (position.IsFrame
                    ? position.FrameNumber + "コマ目(" + position.Time.TotalSeconds.ToString("0.000") + "秒付近)"
                    : position.Time.TotalSeconds.ToString("0.000") + "秒") + "のコマ";
                SetOriginalImage(image, Path.GetFileNameWithoutExtension(videoPath) + "_" + position.ToFileNameSuffix());
            });
        }

        private void SetOriginalImage(BitmapSource image, string saveBaseName)
        {
            _originalImage = image;
            _saveBaseName = saveBaseName;
            UpdateImageView();
        }

        // 元の画像にガンマ補正をかけて表示する
        private void UpdateImageView()
        {
            ImageView.Source = ImageFiles.ApplyGamma(_originalImage, SliderGamma.Value);
        }

        private void SliderGamma_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_gammaTimer == null)
            {
                // InitializeComponentの途中(初期値の設定)で呼ばれたとき
                return;
            }
            _gammaTimer.Stop();
            _gammaTimer.Start();
        }

        private void SliderGamma_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SliderGamma.Value = GammaCorrection.DefaultGamma;
        }

        private void ButtonSavePng_Click(object sender, RoutedEventArgs e)
        {
            BitmapSource shown = ImageView.Source as BitmapSource;
            if (shown == null)
            {
                MessageBox.Show("保存する画像がありません。一覧で画像か動画を選んでください");
                return;
            }

            // 既定の名前は「元のファイル名.png」、動画のコマなら「元のファイル名_12.500s.png」「元のファイル名_f120.png」
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "PNG画像 (*.png)|*.png",
                FileName = (string.IsNullOrEmpty(_saveBaseName) ? "image" : _saveBaseName) + ".png",
            };
            if (Directory.Exists(TextDirName.Text))
            {
                dialog.InitialDirectory = TextDirName.Text;
            }
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                ImageFiles.SavePng(shown, dialog.FileName);
                TextStatus.Text = "保存しました: " + dialog.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存できませんでした" + Environment.NewLine + ex.Message);
            }
        }

        // xamlのScrollViewerのPreviewMouseWheelで定義したメソッド
        // 画像の上でホイールを回すと、マウスの位置を中心に拡大/縮小する(倍率はSliderScaleと連動)。
        // ScrollViewerは自分でホイールを縦スクロールに使ってしまうため、Preview(トンネル)イベントで先に処理して止める
        private void ScrollViewerImage_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;

            double oldScale = SliderScale.Value;
            double newScale = _wheelZoom.Apply(oldScale, e.Delta);
            if (Math.Abs(newScale - oldScale) < 1e-9)
            {
                // 上限・下限に達しているか、1ノッチ分たまっていない
                return;
            }

            // 拡大/縮小する前のスクロール位置とマウス位置を覚えておく(倍率を変えるとスクロール範囲が変わり、位置が補正されるため)
            Point mouse = e.GetPosition(ScrollViewerImage);
            double oldHorizontalOffset = ScrollViewerImage.HorizontalOffset;
            double oldVerticalOffset = ScrollViewerImage.VerticalOffset;
            double ratio = newScale / oldScale;

            SliderScale.Value = newScale;

            // 新しい倍率でのスクロール範囲を確定させてから、マウスの下の点が動かないようにスクロールし直す
            ScrollViewerImage.UpdateLayout();
            ScrollViewerImage.ScrollToHorizontalOffset(WheelZoom.KeepPointUnderMouse(oldHorizontalOffset, mouse.X, ratio));
            ScrollViewerImage.ScrollToVerticalOffset(WheelZoom.KeepPointUnderMouse(oldVerticalOffset, mouse.Y, ratio));
        }
    }
}
