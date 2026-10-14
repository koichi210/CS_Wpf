using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Zodiac
{
    /// <summary>
    /// 生まれた年・年齢・干支の早見画面(MFC版ZodiacDlg)。
    /// 「今年」を選び、閲覧基準(生まれた年/年齢/干支)と下段のリストの項目を選んで「閲覧基準で表示」すると右の欄に結果を出す。
    /// 「一覧表示」は1900年から今年までの干支の早見表をメッセージボックスで出す。
    /// </summary>
    public partial class MainWindow : Window
    {
        // MFC版のAfxMessageBoxはアプリ名(exe名)をキャプションにしていた
        private const string _appName = "Zodiac";

        private ViewMode _mode = ViewMode.None;
        private int _year;  // 閲覧基準の年(今年)

        public MainWindow()
            : this(DateTime.Now.Year)
        {
        }

        // テストから「今年」を固定するための入口
        internal MainWindow(int thisYear)
        {
            InitializeComponent();

            ShowMessage = (text, caption, icon) => MessageBox.Show(this, text, caption, MessageBoxButton.OK, icon);

            // 年の選択肢を作り、今年を選んでおく。
            // MFC版のSetCurSelは選択変更の通知を出さないので、ハンドラは選び終えてから付ける
            foreach (int year in Logic.YearChoices())
            {
                comboBoxYear.Items.Add(year);
            }
            comboBoxYear.SelectedIndex = Logic.YearIndex(thisYear);
            _year = GetSelectedYear();
            comboBoxYear.SelectionChanged += comboBoxYear_SelectionChanged;

            // Checkedイベントから ChangeMode(Age) が呼ばれ、下段のリストが年齢になる
            radioAge.IsChecked = true;

            Loaded += (s, e) => comboBoxYear.Focus();
        }

        // メッセージボックスの表示(テストでは差し替えて、出た内容を確認する)
        internal Func<string, string, MessageBoxImage, MessageBoxResult> ShowMessage { get; set; }

        internal ViewMode Mode
        {
            get { return _mode; }
        }

        internal int Year
        {
            get { return _year; }
        }

        private void comboBoxYear_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _year = GetSelectedYear();
            Refresh();
        }

        private int GetSelectedYear()
        {
            return (int)comboBoxYear.SelectedItem;
        }

        private void radioBirth_Checked(object sender, RoutedEventArgs e)
        {
            ChangeMode(ViewMode.Birth);
        }

        private void radioAge_Checked(object sender, RoutedEventArgs e)
        {
            ChangeMode(ViewMode.Age);
        }

        private void radioZodiac_Checked(object sender, RoutedEventArgs e)
        {
            ChangeMode(ViewMode.Zodiac);
        }

        private void ChangeMode(ViewMode mode)
        {
            if (_mode != mode)
            {
                _mode = mode;
                Refresh();
            }
        }

        // 表示モードに合わせて下段のリストを作り直す(選択も初期値に戻る)
        private void Refresh()
        {
            int selectedIndex;
            List<ListEntry> list = Logic.BuildList(_mode, _year, out selectedIndex);

            comboBoxList.Items.Clear();
            foreach (ListEntry entry in list)
            {
                comboBoxList.Items.Add(entry);
            }
            comboBoxList.SelectedIndex = selectedIndex;
        }

        private void buttonView_Click(object sender, RoutedEventArgs e)
        {
            ListEntry entry = comboBoxList.SelectedItem as ListEntry;
            if (entry == null)
            {
                return;
            }

            bool programError;
            string view = Logic.BuildView(_mode, entry.Value, _year, out programError);
            if (programError)
            {
                // AfxMessageBoxはMB_OKだけ指定すると警告アイコンを付けていた
                ShowMessage(Logic.ProgramErrorStr, _appName, MessageBoxImage.Exclamation);
            }
            textBoxPreview.Text = view;
        }

        private void buttonAllView_Click(object sender, RoutedEventArgs e)
        {
            ShowMessage(Logic.BuildAllView(_year), Logic.AllViewCaption, MessageBoxImage.None);
        }

        private void buttonOk_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
