using Microsoft.Win32;
//using Microsoft.WindowsAPICodePack.Dialogs;
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
        private readonly string m_MsgSelectThumnailDir = "サムネイル表示対象のフォルダ選択";
        private readonly string m_MsgFileFilter = "テキストファイル (*.txt)|*.txt|全てのファイル (*.*)|*.*";
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
            DrawThumnail();
        }

        // xamlのClosingで定義したメソッド
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSetting();
        }

        private void LoadSetting()
        {
            //this.Left = Properties.Settings.Default.ScreenLeft;
            //this.Top = Properties.Settings.Default.ScreenTop;
            //this.Width = Properties.Settings.Default.ScreenWidth;
            //this.Height = Properties.Settings.Default.ScreenHeight;
            //TextDirName.Text = Properties.Settings.Default.ThumnailDirPath;
            //TextFileName.Text = Properties.Settings.Default.TextFileName;
        }

        private void SaveSetting()
        {
            //Properties.Settings.Default.ScreenLeft = this.Left;
            //Properties.Settings.Default.ScreenTop = this.Top;
            //Properties.Settings.Default.ScreenWidth = this.Width;
            //Properties.Settings.Default.ScreenHeight = this.Height;
            //Properties.Settings.Default.ThumnailDirPath = TextDirName.Text;
            //Properties.Settings.Default.TextFileName = TextFileName.Text;

            //Properties.Settings.Default.Save();
        }

        private void ButtonOpenDialog_Click(object sender, RoutedEventArgs e)
        {
            if (!GetDirectoryName())
            {
                MessageBox.Show(m_MsgNotSelectDir);
                return;
            }
            DrawThumnail();
        }

        private void ButtonFormOpenDialog_Click(object sender, RoutedEventArgs e)
        {
            if (!GetDirectoryNameForForm())
            {
                MessageBox.Show(m_MsgNotSelectDir);
                return;
            }
            DrawThumnail();
        }

        // フォルダパスをコピペで指定できるUI
        // CommonOpenFileDialog()を使うためには、NuGetでMicrosoft.WindowsAPICodePack-Shellをインストールする必要がある。
        private Boolean GetDirectoryName()
        {
            Boolean IsSuccess = false;
            //var dlg = new CommonOpenFileDialog(m_MsgSelectThumnailDir)
            //{
            //    IsFolderPicker = true
            //};

            //if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            //{
            //    TextDirName.Text = dlg.FileName;
            //    IsSuccess = true;
            //}
            return IsSuccess;
        }

        // フォルダパスをツリー上で掘っていくUI
        private Boolean GetDirectoryNameForForm()
        {
            Boolean IsSuccess = false;
            var dlg = new System.Windows.Forms.FolderBrowserDialog()
            {
                Description = m_MsgSelectThumnailDir
            };

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TextDirName.Text = dlg.SelectedPath;
                IsSuccess = true;
            }
            return IsSuccess;
        }

        // 本アプリでは未使用。参考までに作成
        private Boolean GetFileName(ref string FileName)
        {
            Boolean IsSuccess = false;
            var dlg = new OpenFileDialog()
            {
                Filter = m_MsgFileFilter
            };

            if (dlg.ShowDialog() == true)
            {
                FileName = dlg.FileName;
                IsSuccess = true;
            }
            return IsSuccess;
        }

        private void DrawThumnail()
        {
            string[] files = null;
            if (!GetFileList(ref files))
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

            var thumnail = new List<Thumnail>();
            foreach (string file in files)
            {
                thumnail.Add(new Thumnail(file));
            }

            DataContext = thumnail;
        }

        private Boolean GetFileList(ref string[] files)
        {
            if (!Directory.Exists(TextDirName.Text))
            {
                return false;
            }

            string[] AllFile = Directory.GetFiles(TextDirName.Text);
            files = AllFile.Where(file => m_TargetExts.Any(pattern => file.ToLower().EndsWith(pattern))).ToArray();
            if (files.Length == 0)
            {
                return false;
            }

            return true;
        }
    }
}
