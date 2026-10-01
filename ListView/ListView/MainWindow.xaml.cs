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
            ExaminationList examinationList = new ExaminationList();
            listView1.DataContext = examinationList.ExaminationResult;
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            List<Examination> examinations = new List<Examination>();

            examinations.Add(new Examination { Subject = "National language", Point = 10, UserName = "Mary", ClassName = "D" });
            examinations.Add(new Examination { Subject = "Society", Point = 30, UserName = "Bob", ClassName = "D" });
            examinations.Add(new Examination { Subject = "Society", Point = 40, UserName = "John", ClassName = "D" });

            listView2.ItemsSource = examinations;
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            List<Examination> examinations = new List<Examination>();

            examinations.Add(new Examination { Subject = "Society", Point = 100, UserName = "Samantha", ClassName = "S" });
            examinations.Add(new Examination { Subject = "Mathematics", Point = 100, UserName = "Zelda", ClassName = "S" });
            examinations.Add(new Examination { Subject = "National language", Point = 100, UserName = "Nataly", ClassName = "S" });

            listView3.ItemsSource = examinations;
        }
    }
}
