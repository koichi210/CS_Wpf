using System;
using System.IO;
using System.Windows;
using SQLite.Objects;

namespace SQLite
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string _databaseName = "SqLiteSample.db";
        private readonly string _databasePath;

        public MainWindow()
        {
            InitializeComponent();

            _databasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _databaseName);
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
            using (var connection = new SQLiteConnection(_databasePath))
            {
                connection.CreateTable<Customer>();
                connection.Insert(customer);
            }
        }

        private void ReadButton_Click(object sender, RoutedEventArgs e)
        {
            // sqlite3のDLLが読み込めずうまく動かない
            //using (var connection = new SQLiteConnection(_databasePath))
            //{
            //    connection.CreateTable<Customer>();
            //    var customers = connection.Table<Customer>().ToList();
            //}
        }
    }
}
