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
using System.Collections.ObjectModel;

namespace ComboBoxForWpf
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // 要素がデータバインディングに含まれている場合に、その要素のデータコンテキストを取得または設定
            this.DataContext = new PrefectureList();
        }
    }

    public class PrefectureList
    {
        // バインディングの指定先プロパティ
        public ObservableCollection<String> Prefectures { get; set; }

        // コンストラクタ(データ入力)
        public PrefectureList()
        {
            Prefectures = new ObservableCollection<String>();
            Prefectures.Add("Tokyo");
            Prefectures.Add("Osaka");
            Prefectures.Add("Fukuoka");
            Prefectures.Add("Kanagawa");
            Prefectures.Add("Hokkaido");
        }
    }
}
