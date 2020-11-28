using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SaveRestoreForWpf
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadSetting();
        }

        // xamlのClosingで定義したメソッド。ウィンドウ閉じるときに呼ばれる
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSetting();
        }

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            SaveSetting();
        }

        private void buttonLoad_Click(object sender, RoutedEventArgs e)
        {
            LoadSetting();
        }

        private void buttonReset_Click(object sender, RoutedEventArgs e)
        {
            // 設定をアプリの初期値に戻す
            Properties.Settings.Default.Reset();
        }

        private void LoadSetting()
        {
            //  設定をロード。不要（？）
            //Properties.Settings.Default.Reload();

            // 画面サイズ
            this.Left = Properties.Settings.Default.ScreenLeft;
            this.Top = Properties.Settings.Default.ScreenTop;
            this.Width = Properties.Settings.Default.ScreenWidth;
            this.Height = Properties.Settings.Default.ScreenHeight;
            
            // 設定値をコントロールに配置
            textBoxPath.Text = Properties.Settings.Default.textBoxPath;
        }

        private void SaveSetting()
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
