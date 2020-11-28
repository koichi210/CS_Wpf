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

namespace ListViewForWpf
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
            ExaminationList el = new ExaminationList();
            listView1.DataContext = el.ExaminationResult;
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            List<Examination> el = new List<Examination>();

            el.Add(new Examination { Subject = "National language", Point = 10, UserName = "Mary", ClassName = "D" });
            el.Add(new Examination { Subject = "Society", Point = 30, UserName = "Bob", ClassName = "D" });
            el.Add(new Examination { Subject = "Society", Point = 40, UserName = "John", ClassName = "D" });

            listView2.ItemsSource = el;
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            List<Examination> el = new List<Examination>();

            el.Add(new Examination { Subject = "Society", Point = 100, UserName = "Samantha", ClassName = "S" });
            el.Add(new Examination { Subject = "Mathematics", Point = 100, UserName = "Zelda", ClassName = "S" });
            el.Add(new Examination { Subject = "National language", Point = 100, UserName = "Nataly", ClassName = "S" });

            listView3.ItemsSource = el;
        }
    }
}
