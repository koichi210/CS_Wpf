using System;
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
            if (textBox_Directory.Text == String.Empty)
            {
                MessageBox.Show("対象ファイルのディレクトリが設定されていません");
                return;
            }

            //リストアップ
            String[] targetFiles = Logic.GetTargetFiles(textBox_Directory.Text, textBox_File.Text, checkBox_SubDirectory.IsChecked == true);

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
                _fio.SaveFile(filePath, resultData);
            }
        }
    }
}
