using System;
using System.IO;
using System.Windows;
using StandardTemplate;

namespace ToyingFile
{
    public partial class MainWindow : Window
    {
        // WinForms版と同じくShift_JISで読み書きする(StcFileInputOutput.LoadFile/SaveFile)
        private readonly StcFileInputOutput _fio = new StcFileInputOutput();

        public MainWindow()
        {
            InitializeComponent();

            // WinForms版ではCtrl+Aの全選択をKeyDownで自前処理していたが、WPFのTextBoxは標準でCtrl+Aが全選択なので不要。
            // 入力例(プレースホルダー)はXAML側で付けている。削除文字列欄はWinForms版(複数行の欄はWindowsの仕様で
            // 表示されなかった)に合わせて付けていない
        }

        private void button_Execute_Click(object sender, RoutedEventArgs e)
        {
            Execute();
        }

        internal void Execute()
        {
            String error = Logic.ValidateDirectory(textBox_Directory.Text);
            if (error != null)
            {
                MessageBox.Show(error);
                return;
            }

            //リストアップ
            // ディレクトリがあっても、アクセス権の無いサブフォルダやパターンに使えない文字があると列挙の途中で失敗する
            String[] targetFiles;
            try
            {
                targetFiles = Logic.GetTargetFiles(textBox_Directory.Text, textBox_File.Text, checkBox_SubDirectory.IsChecked == true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                MessageBox.Show("対象ファイルを列挙できませんでした" + Environment.NewLine + ex.Message);
                return;
            }

            // メニュー
            if (radioButton_DeleteString.IsChecked == true)
            {
                DeleteStringFromFiles(targetFiles);
            }
        }

        private void DeleteStringFromFiles(String[] filePaths)
        {
            // WPFのTextBoxもEnterで入る改行はEnvironment.NewLine(CRLF)なので、WinForms版と同じ区切りで分けられる
            String[] deleteStrings = Logic.SplitDeleteStrings(textBox_DeleteString.Text);

            foreach (String filePath in filePaths)
            {
                String fileData = _fio.LoadFile(filePath);
                String resultData = Logic.DeleteStringFromContent(fileData, deleteStrings,
                    checkBox_CaseSensitive.IsChecked == true, checkBox_DeleteLine.IsChecked == true);

                // 削除する文字列が無かったファイルは書き戻さない。以前は対象の全ファイルをShift_JISで書き直していたため、
                // 一致しなかったUTF-8のテキストや画像等のバイナリ(ファイル名パターン空欄=全ファイル)まで化けて壊れていた
                if (resultData == fileData)
                {
                    continue;
                }
                _fio.SaveFile(filePath, resultData);
            }
        }
    }
}
