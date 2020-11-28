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
using System.Windows.Shapes;

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
