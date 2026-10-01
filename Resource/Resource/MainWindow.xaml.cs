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
            FormChild formChild = new FormChild();
            formChild.Show();
        }

        private void button6_Click(object sender, RoutedEventArgs e)
        {
            WindowChild windowChild = new WindowChild();
            windowChild.Show();
        }
    }
}
