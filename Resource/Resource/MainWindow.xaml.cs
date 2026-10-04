using System.Windows;

namespace Resource
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void button5_Click(object sender, RoutedEventArgs e)
        {
            new FormChild().Show();
        }

        private void button6_Click(object sender, RoutedEventArgs e)
        {
            new WindowChild().Show();
        }
    }
}
