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
            // 最小化中・最大化中に閉じると、Width/Heightはタスクバー上の大きさ(160×28程度)や画面いっぱいの大きさになり、
            // 次回起動時に極小・画面サイズのウィンドウで開いてしまう。通常表示のときの位置と大きさ(RestoreBounds)を保存する
            Rect bounds = (WindowState == WindowState.Normal) ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            settings.ScreenLeft = bounds.Left;
            settings.ScreenTop = bounds.Top;
            settings.ScreenWidth = bounds.Width;
            settings.ScreenHeight = bounds.Height;
            settings.textBoxPath = textBoxPath.Text;

            // 設定を保存
            settings.Save();
        }
    }
}
