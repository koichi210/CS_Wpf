using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace ListViewFilter
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<Examination> _examinations = new ObservableCollection<Examination>();
        private int _registeredCount;
        
        public MainWindow()
        {
            InitializeComponent();

            for (int i = 0; i < 15; i++)
            {
                AddExamination();
            }
            CustomerListView.ItemsSource = _examinations;
        }

        private void AddExamination()
        {
            _examinations.Add(new Examination { Id = ++_registeredCount, Subject = "Subject" + _registeredCount, Point = 0, UserName = "UserName" + _registeredCount });
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            AddExamination();

            // リストを更新するとフィルタが無効になる
            //CustomerListView.ItemsSource = _examinations;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var filterList = _examinations.Where(x => x.UserName.Contains(SearchTextBox.Text)).ToList();
            CustomerListView.ItemsSource = filterList;
        }
    }
}
