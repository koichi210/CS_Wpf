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

namespace ListViewFilterForWpf
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<Examination> m_examinations = new ObservableCollection<Examination>();
        private int m_regist_num = 0;
        
        public MainWindow()
        {
            InitializeComponent();

            for (int i = 0; i < 15; i++)
            {
                AddList();
            }
            CustomerListView.ItemsSource = m_examinations;
        }

        private void AddList()
        {
            m_examinations.Add(new Examination { Id = ++m_regist_num, Subject = "Subject" + m_regist_num, Point = 0, UserName = "UserName" + m_regist_num });
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            AddList();

            // リストを更新するとフィルタが無効になる
            //CustomerListView.ItemsSource = _customers;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var filterList = m_examinations.Where(x => x.UserName.Contains(SearchTextBox.Text)).ToList();
            CustomerListView.ItemsSource = filterList;
        }
    }
}
