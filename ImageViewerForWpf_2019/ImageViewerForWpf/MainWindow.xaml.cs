using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Windows;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace ImageViewerForWpf
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string[] m_TargetExts = { ".jpg", ".bmp", ".png", ".tiff", ".gif" };

        private readonly string m_MsgNotSelectDir = "フォルダが選択されていません";
        private readonly string m_MsgSelectThumbnailDir = "サムネイル表示対象のフォルダ選択";
        private readonly string m_FileFilter = "テキストファイル (*.txt)|*.txt|全てのファイル (*.*)|*.*";
        private readonly string m_MsgUnknownDir = "サムネイルを表示するフォルダが存在しないか、画像ファイルがありません。";

        public MainWindow()
        {
            InitializeComponent();
        }

        ~MainWindow()
        {
            // ウィンドウが消滅しているので、ここでは取得できない
            //SaveSetting();
        }

        // xamlのLoadedで定義したメソッド
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            LoadSetting();
            DrawThumbnails();
        }

        // xamlのClosingで定義したメソッド
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSetting();
        }

        private void LoadSetting()
        {
            this.Left = Properties.Settings.Default.ScreenLeft;
            this.Top = Properties.Settings.Default.ScreenTop;
            this.Width = Properties.Settings.Default.ScreenWidth;
            this.Height = Properties.Settings.Default.ScreenHeight;
            TextDirName.Text = Properties.Settings.Default.ThumnailDirPath;
            TextFileName.Text = Properties.Settings.Default.TextFileName;
        }

        private void SaveSetting()
        {
            Properties.Settings.Default.ScreenLeft = this.Left;
            Properties.Settings.Default.ScreenTop = this.Top;
            Properties.Settings.Default.ScreenWidth = this.Width;
            Properties.Settings.Default.ScreenHeight = this.Height;
            Properties.Settings.Default.ThumnailDirPath = TextDirName.Text;
            Properties.Settings.Default.TextFileName = TextFileName.Text;

            Properties.Settings.Default.Save();
        }

        private void ButtonOpenDialog_Click(object sender, RoutedEventArgs e)
        {
            if (!TrySelectDirectory())
            {
                MessageBox.Show(m_MsgNotSelectDir);
                return;
            }
            DrawThumbnails();
        }

        private void ButtonFormOpenDialog_Click(object sender, RoutedEventArgs e)
        {
            if (!TrySelectDirectoryForForm())
            {
                MessageBox.Show(m_MsgNotSelectDir);
                return;
            }
            DrawThumbnails();
        }

        // フォルダパスをコピペで指定できるUI
        private Boolean TrySelectDirectory()
        {
            Boolean isSuccess = false;
            var dlg = new CommonOpenFileDialog(m_MsgSelectThumbnailDir)
            {
                IsFolderPicker = true
            };

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                TextDirName.Text = dlg.FileName;
                isSuccess = true;
            }
            return isSuccess;
        }

        // フォルダパスをツリー上で掘っていくUI
        private Boolean TrySelectDirectoryForForm()
        {
            Boolean isSuccess = false;
            var dlg = new System.Windows.Forms.FolderBrowserDialog()
            {
                Description = m_MsgSelectThumbnailDir
            };

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TextDirName.Text = dlg.SelectedPath;
                isSuccess = true;
            }
            return isSuccess;
        }

        // 本アプリでは未使用。参考までに作成
        private Boolean TrySelectFile(ref string fileName)
        {
            Boolean isSuccess = false;
            var dlg = new OpenFileDialog()
            {
                Filter = m_FileFilter
            };

            if (dlg.ShowDialog() == true)
            {
                fileName = dlg.FileName;
                isSuccess = true;
            }
            return isSuccess;
        }

        private void DrawThumbnails()
        {
            string[] files = null;
            if (!TryGetImageFiles(ref files))
            {
                MessageBox.Show(m_MsgUnknownDir + Environment.NewLine + this.TextDirName.Text);
                return;
            }

            // クラスをローカル変数にしたので、クリア処理は不要
            //if (ListBoxTumnail.Items.Count != 0)
            //{
            //    Exception
            //    ListBoxTumnail.Items.Clear();

            //    Never displayed
            //    ListBoxTumnail.ClearValue(ListBox.ItemsSourceProperty);
            //}

            var thumbnails = new List<Thumbnail>();
            foreach (string file in files)
            {
                thumbnails.Add(new Thumbnail(file));
            }

            DataContext = thumbnails;
        }

        private Boolean TryGetImageFiles(ref string[] files)
        {
            if (! Directory.Exists(TextDirName.Text))
            {
                return false;
            }

            string[] allFiles = Directory.GetFiles(TextDirName.Text);
            files = allFiles.Where(file => m_TargetExts.Any(pattern => file.ToLower().EndsWith(pattern))).ToArray();
            return files.Length != 0;
        }
    }
}