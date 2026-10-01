using System.Windows;

namespace ResourceForWpf
{
    /// <summary>
    /// WindowChild.xaml の相互作用ロジック
    /// </summary>
    public partial class WindowChild : Window
    {
        public WindowChild()
        {
            InitializeComponent();
        }

        private void ChildButton1_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("WPFダイアログのボタンがクリックされました");
        }
    }
}
