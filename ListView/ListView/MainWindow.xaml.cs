using System.Collections.Generic;
using System.Windows;

namespace ListView
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

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            listView1.DataContext = new ExaminationList().ExaminationResult;
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            listView2.ItemsSource = new List<Examination>
            {
                new Examination { Subject = "National language", Point = 10, UserName = "Mary", ClassName = "D" },
                new Examination { Subject = "Society", Point = 30, UserName = "Bob", ClassName = "D" },
                new Examination { Subject = "Society", Point = 40, UserName = "John", ClassName = "D" },
            };
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            listView3.ItemsSource = new List<Examination>
            {
                new Examination { Subject = "Society", Point = 100, UserName = "Samantha", ClassName = "S" },
                new Examination { Subject = "Mathematics", Point = 100, UserName = "Zelda", ClassName = "S" },
                new Examination { Subject = "National language", Point = 100, UserName = "Nataly", ClassName = "S" },
            };
        }
    }
}
