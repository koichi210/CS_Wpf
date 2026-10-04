using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CubNotice
{
    public partial class MainWindow : Window
    {
        private readonly EventStore store = new EventStore(DataLocation.EventsFile);
        private AppSettings settings;

        public MainWindow()
        {
            InitializeComponent();

            settings = AppSettings.Load(DataLocation.SettingsFile);
            store.Load();

            PageNumberBox.Text = settings.PageNumber.ToString();
            HeaderBox.Text = settings.DefaultHeader;
            TemplateBox.Text = settings.Template;
            PlaceholderHelp.Text = "差し込み項目: " + string.Join("  ", AnnouncementFormatter.Placeholders)
                + "\nデータの保存先: " + DataLocation.GetFolder();
            RefreshGrid();
        }

        // ---- データ追加フェーズ ----

        private void OpenPdfButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "PDFファイル (*.pdf)|*.pdf|すべてのファイル (*.*)|*.*",
                Title = "カブ8通信のPDFを選択",
            };
            if (dialog.ShowDialog(this) == true)
            {
                ImportPdf(dialog.FileName);
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = GetDroppedPdf(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            string path = GetDroppedPdf(e);
            if (path != null)
            {
                ImportPdf(path);
            }
        }

        private static string GetDroppedPdf(DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            return files?.FirstOrDefault(f => string.Equals(Path.GetExtension(f), ".pdf", StringComparison.OrdinalIgnoreCase));
        }

        private void ImportPdf(string path)
        {
            int page;
            if (!int.TryParse(PageNumberBox.Text, out page) || page < 1)
            {
                MessageBox.Show(this, "ページ番号は1以上の数字で入力してください。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                List<string> lines = PdfTextExtractor.ExtractLines(path, page);
                if (settings.PageNumber != page)
                {
                    settings.PageNumber = page;
                    settings.Save(DataLocation.SettingsFile);
                }
                ImportLines(lines, Path.GetFileName(path) + " の " + page + "ページ目");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "PDFを読み込めませんでした。\n" + ex.Message
                    + "\n\nPDFビューアで文字をコピーして「クリップボードの文字から取込」も試してみてください。",
                    Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PasteTextButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Clipboard.ContainsText())
            {
                MessageBox.Show(this, "クリップボードに文字がありません。", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string text = Clipboard.GetText();
            ImportLines(text.Replace("\r\n", "\n").Split('\n').ToList(), "クリップボードの文字");
        }

        private void ImportLines(List<string> lines, string sourceName)
        {
            ParseResult result = NewsletterParser.Parse(lines, settings.DefaultHeader, DateTime.Today);
            if (result.Events.Count == 0)
            {
                MessageBox.Show(this, sourceName + "から予定(「☆ 月日(曜)」で始まり、集合・解散などがあるもの)が見つかりませんでした。\n\n"
                    + "読み取った文字(先頭20行):\n" + string.Join("\n", lines.Take(20)),
                    Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Tuple<int, int> counts = store.AddOrUpdate(result.Events);
            store.Save();
            RefreshGrid();
            MainTab.SelectedIndex = 0;

            string message = string.Format("{0}から予定を取り込みました。(追加 {1}件 / 上書き {2}件)", sourceName, counts.Item1, counts.Item2);
            if (result.SkippedLines.Count > 0)
            {
                message += string.Format("  ※詳細の無い予定 {0}件はスキップ", result.SkippedLines.Count);
            }
            StatusText.Text = message;
        }

        // ---- 一覧の編集 ----

        private void EventGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
            {
                return;
            }
            // 編集内容がバインド先に反映された後で保存する
            Dispatcher.BeginInvoke(new Action(() =>
            {
                store.Save();
                StatusText.Text = "変更を保存しました。";
            }), DispatcherPriority.Background);
        }

        private void DeleteRowButton_Click(object sender, RoutedEventArgs e)
        {
            List<CubEvent> selected = EventGrid.SelectedItems.OfType<CubEvent>().ToList();
            if (selected.Count == 0)
            {
                return;
            }
            if (MessageBox.Show(this, selected.Count + "件の予定を削除しますか？", Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            {
                return;
            }
            store.Events.RemoveAll(selected.Contains);
            store.Save();
            RefreshGrid();
            StatusText.Text = selected.Count + "件の予定を削除しました。";
        }

        private void RemovePastButton_Click(object sender, RoutedEventArgs e)
        {
            int count = RemovePastEvents();
            StatusText.Text = count == 0 ? "過ぎた予定はありませんでした。" : count + "件の過ぎた予定を削除しました。";
        }

        // ---- データ破棄フェーズ ----

        private int RemovePastEvents()
        {
            List<CubEvent> removed = store.RemovePast(DateTime.Today);
            if (removed.Count > 0)
            {
                store.Save();
                RefreshGrid();
            }
            return removed.Count;
        }

        // ---- データ取得フェーズ ----

        private void MakeNextButton_Click(object sender, RoutedEventArgs e)
        {
            EventGrid.CommitEdit(DataGridEditingUnit.Row, true);
            int removed = RemovePastEvents();
            CubEvent next = store.FindNext(DateTime.Today);
            string removedText = removed > 0 ? string.Format("(過ぎた予定 {0}件を削除しました)", removed) : "";
            if (next == null)
            {
                AnnouncementBox.Text = "";
                NextInfoText.Text = "次回の予定がありません。新しいカブ8通信を取り込んでください。" + removedText;
                return;
            }
            AnnouncementBox.Text = AnnouncementFormatter.Format(next, settings.Template);
            NextInfoText.Text = string.Format("{0:yyyy/MM/dd} の予定です。{1}", next.Date, removedText);
            StatusText.Text = "次回開催案内を作成しました。";
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (AnnouncementBox.Text.Length == 0)
            {
                return;
            }
            Clipboard.SetText(AnnouncementBox.Text);
            StatusText.Text = "案内文をクリップボードにコピーしました。";
        }

        // ---- 設定 ----

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            StatusText.Text = "設定を保存しました。";
        }

        private void ResetTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            TemplateBox.Text = AnnouncementFormatter.DefaultTemplate;
        }

        private void ApplyHeaderButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            foreach (CubEvent cubEvent in store.Events)
            {
                cubEvent.Header = settings.DefaultHeader;
            }
            store.Save();
            RefreshGrid();
            StatusText.Text = "固定ヘッダを全予定に反映しました。";
        }

        private void OpenDataFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string folder = DataLocation.GetFolder();
            Directory.CreateDirectory(folder);
            Process.Start("explorer.exe", "\"" + folder + "\"");
        }

        private void SaveSettings()
        {
            settings.DefaultHeader = HeaderBox.Text;
            settings.Template = TemplateBox.Text;
            settings.Save(DataLocation.SettingsFile);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            EventGrid.CommitEdit(DataGridEditingUnit.Row, true);
            store.Save();
        }

        private void RefreshGrid()
        {
            EventGrid.ItemsSource = null;
            EventGrid.ItemsSource = store.Events;
        }
    }
}
