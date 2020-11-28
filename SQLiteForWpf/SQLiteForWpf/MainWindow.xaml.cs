using System;
using System.IO;
using System.Windows;
using SQLite;
using SQLiteForWpf.Objects;

namespace SQLiteForWpf
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly String databaseName = "SqLiteSample.db";
        private String DatabasePath = "";

        public MainWindow()
        {
            InitializeComponent();

            String folderPath = AppDomain.CurrentDomain.BaseDirectory;
            DatabasePath = Path.Combine(folderPath, databaseName);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // GUIの設定値をメンバ変数に設定
            var customer = new Customer()
            {
                Name = NameTextBox.Text,
                Phone = PhoneTextBox.Text,
            };

            // sqlite3のDLLが読み込めずうまく動かない
            using (var connection = new SQLiteConnection(DatabasePath))
            {
                connection.CreateTable<Customer>();
                connection.Insert(customer);
            }
        }

        private void ReadButton_Click(object sender, RoutedEventArgs e)
        {
            // sqlite3のDLLが読み込めずうまく動かない
            //using (var connection = new SQLiteConnection(DatabasePath))
            //{
            //    connection.CreateTable<Customer>();
            //    var customers = connection.Table<Customer>().ToList();
            //}
        }
    }
}
