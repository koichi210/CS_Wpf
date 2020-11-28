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

namespace TextBoxForWpf
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public String text { get; set; }


        public MainWindow()
        {
            InitializeComponent();
            TextString1 ts1 = new TextString1();
            ts1.txt1 = "Hello World";
            textBox1.DataContext = ts1;

            TextString2 ts2 = new TextString2();
            textBox2.DataContext = ts2;

            textBox3.DataContext = new TextString3();

            textBox4.Text = "バインドせずに設定";
        }
    }

    public class TextString1
    {
        public String txt1 { get; set; }

        // コンストラクタ(データ入力)
        public TextString1()
        {
        }
    }

    public class TextString2
    {
        public String txt2 { get; set; }

        // コンストラクタ(データ入力)
        public TextString2()
        {
            txt2 = "Hola!!";
        }
    }

    public class TextString3
    {
        public String txt3 { get; set; }

        // コンストラクタ(データ入力)
        public TextString3()
        {
            txt3 = "World!!";
        }
    }
}
