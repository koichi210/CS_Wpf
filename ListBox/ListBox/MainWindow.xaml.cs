using System.Collections.ObjectModel;
using System.Windows;

namespace ListBox
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
            DataContext = new PrefectureList();
        }
    }

    public class PrefectureList
    {
        // バインディングの指定先プロパティ
        public ObservableCollection<string> Prefectures { get; set; } = new ObservableCollection<string>
        {
            "Tokyo",
            "Osaka",
            "Fukuoka",
            "Kanagawa",
            "Hokkaido",
        };
    }
}
