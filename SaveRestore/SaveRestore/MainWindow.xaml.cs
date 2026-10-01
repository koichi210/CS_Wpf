using System.Windows;

namespace SaveRestore
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        // xamlのClosingで定義したメソッド。ウィンドウ閉じるときに呼ばれる
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
        }

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void buttonLoad_Click(object sender, RoutedEventArgs e)
        {
            LoadSettings();
        }

        private void buttonReset_Click(object sender, RoutedEventArgs e)
        {
            // 設定をアプリの初期値に戻す
            Properties.Settings.Default.Reset();
        }

        private void LoadSettings()
        {
            // 画面サイズ
            this.Left = Properties.Settings.Default.ScreenLeft;
            this.Top = Properties.Settings.Default.ScreenTop;
            this.Width = Properties.Settings.Default.ScreenWidth;
            this.Height = Properties.Settings.Default.ScreenHeight;

            // 設定値をコントロールに配置
            textBoxPath.Text = Properties.Settings.Default.textBoxPath;
        }

        private void SaveSettings()
        {
            // Default設定を変更
            Properties.Settings.Default.ScreenLeft = this.Left;
            Properties.Settings.Default.ScreenTop = this.Top;
            Properties.Settings.Default.ScreenWidth = this.Width;
            Properties.Settings.Default.ScreenHeight = this.Height;
            Properties.Settings.Default.textBoxPath = textBoxPath.Text;

            // 設定を保存
            Properties.Settings.Default.Save();
        }
    }
}
