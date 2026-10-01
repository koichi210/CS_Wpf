using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace ListViewFilterForWpf
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<Examination> m_examinations = new ObservableCollection<Examination>();
        private int m_registeredCount = 0;
        
        public MainWindow()
        {
            InitializeComponent();

            for (int i = 0; i < 15; i++)
            {
                AddExamination();
            }
            CustomerListView.ItemsSource = m_examinations;
        }

        private void AddExamination()
        {
            m_examinations.Add(new Examination { Id = ++m_registeredCount, Subject = "Subject" + m_registeredCount, Point = 0, UserName = "UserName" + m_registeredCount });
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            AddExamination();

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
