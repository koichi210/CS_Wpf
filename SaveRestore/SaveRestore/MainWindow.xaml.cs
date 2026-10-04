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
            var settings = Properties.Settings.Default;

            // 画面サイズ
            Left = settings.ScreenLeft;
            Top = settings.ScreenTop;
            Width = settings.ScreenWidth;
            Height = settings.ScreenHeight;

            // 設定値をコントロールに配置
            textBoxPath.Text = settings.textBoxPath;
        }

        private void SaveSettings()
        {
            // Default設定を変更
            var settings = Properties.Settings.Default;
            settings.ScreenLeft = Left;
            settings.ScreenTop = Top;
            settings.ScreenWidth = Width;
            settings.ScreenHeight = Height;
            settings.textBoxPath = textBoxPath.Text;

            // 設定を保存
            settings.Save();
        }
    }
}
