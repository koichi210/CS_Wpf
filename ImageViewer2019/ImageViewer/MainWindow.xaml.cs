using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ImageViewer
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        // 拡張子の大文字小文字は区別しない
        private static readonly HashSet<string> _targetExts =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".bmp", ".png", ".tiff", ".gif" };

        private const string _msgNotSelectDir = "フォルダが選択されていません";
        private const string _msgSelectThumbnailDir = "サムネイル表示対象のフォルダ選択";
        private const string _fileFilter = "テキストファイル (*.txt)|*.txt|全てのファイル (*.*)|*.*";
        private const string _msgUnknownDir = "サムネイルを表示するフォルダが存在しないか、画像ファイルがありません。";

        // マウスホイールでの拡大/縮小の倍率計算(1ノッチ未満の回転量を溜めておくので、インスタンスを使い回す)
        private readonly WheelZoom _wheelZoom = new WheelZoom();

        public MainWindow()
        {
            InitializeComponent();
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
            ShowThumbnailsOrWarn(TrySelectDirectory());
        }

        private void ButtonFormOpenDialog_Click(object sender, RoutedEventArgs e)
        {
            ShowThumbnailsOrWarn(TrySelectDirectoryForForm());
        }

        private void ShowThumbnailsOrWarn(bool isSelected)
        {
            if (!isSelected)
            {
                MessageBox.Show(_msgNotSelectDir);
                return;
            }
            DrawThumbnails();
        }

        // フォルダパスをコピペで指定できるUI
        private bool TrySelectDirectory()
        {
            var dlg = new CommonOpenFileDialog(_msgSelectThumbnailDir)
            {
                IsFolderPicker = true
            };

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

            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return false;
            }
            TextDirName.Text = dlg.SelectedPath;
            return true;
        }

        // 本アプリでは未使用。参考までに作成
        private bool TrySelectFile(out string fileName)
        {
            var dlg = new OpenFileDialog()
            {
                Filter = _fileFilter
            };

            bool isSelected = dlg.ShowDialog() == true;
            fileName = isSelected ? dlg.FileName : null;
            return isSelected;
        }

        private void DrawThumbnails()
        {
            if (!TryGetImageFiles(out string[] files))
            {
                MessageBox.Show(_msgUnknownDir + Environment.NewLine + TextDirName.Text);
                return;
            }

            // 毎回新しいリストを DataContext に設定するので、既存アイテムのクリア処理は不要
            DataContext = files.Select(file => new Thumbnail(file)).ToList();
        }

        private bool TryGetImageFiles(out string[] files)
        {
            files = null;
            if (!Directory.Exists(TextDirName.Text))
            {
                return false;
            }

            files = Directory.GetFiles(TextDirName.Text)
                .Where(file => _targetExts.Contains(Path.GetExtension(file)))
                .ToArray();
            return files.Length != 0;
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
