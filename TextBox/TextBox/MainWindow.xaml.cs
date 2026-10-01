using System;
using System.Windows;

namespace TextBox
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            TextString1 textString1 = new TextString1();
            textString1.txt1 = "Hello World";
            textBox1.DataContext = textString1;

            TextString2 textString2 = new TextString2();
            textBox2.DataContext = textString2;

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
