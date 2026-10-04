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
            textBox1.DataContext = new TextString1 { Text1 = "Hello World" };
            textBox2.DataContext = new TextString2();
            textBox3.DataContext = new TextString3();

            textBox4.Text = "バインドせずに設定";
        }
    }

    public class TextString1
    {
        public string Text1 { get; set; }
    }

    public class TextString2
    {
        // 初期値を持つプロパティ
        public string Text2 { get; set; } = "Hola!!";
    }

    public class TextString3
    {
        // 初期値を持つプロパティ
        public string Text3 { get; set; } = "World!!";
    }
}
