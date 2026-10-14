using System;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StandardTemplate;
using StandardTemplate.Wpf;

namespace TrimHtmlData
{
    /// <summary>
    /// 複数のURLのHTMLを取得し、検索文字を含む行(とそこから指定行数)だけを抜き出すツール。
    /// WinForms版(CS_Form\TrimHtmlData)のWPF移植。
    /// WinForms版が複数行欄・検索文字欄のKeyDownで自前実装していたCtrl+Aの全選択は、WPFのTextBoxが標準で持っているので移植していない。
    /// </summary>
    public partial class MainWindow : Window
    {
        // データ保存先・設定ファイル名はWinForms版TrimHtmlDataと同じ(%LOCALAPPDATA%\TrimHtmlData\TrimHtmlData.json)。
        // 保存キーも同じにしてあるので、WinForms版で保存したJSONプロファイルをそのまま読める(逆も同じ)
        private const String _appName = "TrimHtmlData";
        private const String _settingFileName = "TrimHtmlData.json";

        // WinForms版は旧XML(*.xml)のプロファイルも一覧に出していたが、WpfSaveRestoreはXMLを読めないのでJSONだけにする
        // (旧XMLからJSONへの移行はWinForms版の起動時に済んでいる前提。Mailer/FileArranger等のWPF版と同じ方針)
        private static readonly String[] _profileExtensions = { "*.json" };

        private readonly String _userDataFolder;
        private readonly StcUtils _util = new StcUtils();
        private readonly StcFileInputOutput _fio = new StcFileInputOutput();
        internal WpfSaveRestore SaveRestore { get; } = new WpfSaveRestore();

        // デバッグモード中は、取得したHTMLと抽出結果をexeの場所へ連番ファイルで書き出す(StcDebug)。
        // 画面の余白をダブルクリックするたびに切り替わる(WinForms版と同じ隠し機能)
        internal Boolean IsDebug { get; private set; } = false;

        // URLからHTMLを取得する処理と、結果をクリップボードへ送る処理。
        // テストで実サイトへ通信したり本物のクリップボードを書き換えたりしないよう、差し替えられるようにしてある
        internal Func<String, String> FetchHtmlSource { get; set; }
        internal Func<String, Boolean> CopyToClipboard { get; set; }

        public MainWindow() : this(UserDataLocation.GetUserDataFolder(_appName))
        {
        }

        // テスト用に、データ保存先を差し替えられるようにしてある(ユーザーの実データフォルダに触れないため)
        internal MainWindow(String dataFolder)
        {
            _userDataFolder = dataFolder;
            FetchHtmlSource = GetHtmlSource;
            CopyToClipboard = _util.SetClipboardText;

            InitializeComponent();
            // デバッグ出力の書き出し先をexeの場所にする(WinForms版のInitializeCommonSettingsと同じ)
            _util.SetCurrentDirectory();

            RegisterSettingItems();

            // 起動時は既定の設定ファイル(TrimHtmlData.json)を読む。無ければ既定値(空欄・チェック無し)
            SaveRestore.LoadOrDefault(Path.Combine(_userDataFolder, _settingFileName));
            // WinForms版と同じく既定の設定ファイルを選んだ状態にする(SelectionChangedでもう一度読み込まれる)
            WpfProfile.UpdateProfileList(comboBox_LoadSetting, _profileExtensions, _settingFileName, _userDataFolder);

            WpfDataFolderMenu.Attach(this, () => DataFolderMenu.ChangeDataFolder(_appName, _userDataFolder,
                (oldFolder, newFolder) => DataFolderMenu.MoveProfiles(oldFolder, newFolder, _appName)));
        }

        private void RegisterSettingItems()
        {
            // キーはWinForms版(SaveRestore.RegisterItem)と同じにすること。
            // 「最初にヒットしたワードだけを抽出」はWinForms版でも保存対象外だったので、そのまま登録しない
            // (足すとWinForms版との間で保存内容が食い違うため)
            SaveRestore.RegisterCtrl("Common", "textBox_SourceList", textBox_SourceList);
            SaveRestore.RegisterCtrl("Common", "textBox_DestList", textBox_DestList);
            SaveRestore.RegisterCtrl("Common", "textBox_SearchWord", textBox_SearchWord);
            SaveRestore.RegisterCtrl("Common", "textBox_DelimiterWord", textBox_DelimiterWord);
            SaveRestore.RegisterCtrl("Common", "textBox_TrimLineNum", textBox_TrimLineNum);
            SaveRestore.RegisterCtrl("Common", "checkBox_OrdinalCase", checkBox_OrdinalCase);
        }

        private void layoutRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // WinForms版のForm.DoubleClickはコントロールの無い所でしか起きないので、余白(layoutRoot自身)のときだけ反応する。
            // WPFのWindow.MouseDoubleClickだとテキストボックス内のダブルクリック(単語選択)でも起きてしまうため使わない
            if (e.ClickCount == 2 && e.OriginalSource == layoutRoot)
            {
                ToggleDebugMode();
                MessageBox.Show("IsDebug=" + IsDebug.ToString());
            }
        }

        internal void ToggleDebugMode()
        {
            IsDebug = !IsDebug;
        }

        private void button_Execute_Click(object sender, RoutedEventArgs e)
        {
            ExecuteTrim();
        }

        // 実行ボタンの処理本体。URLごとに「◆URL」の見出しと抽出結果を並べて取得値欄に出し、クリップボードにもコピーする
        internal void ExecuteTrim()
        {
            // 出力先をクリア
            textBox_DestList.Text = "";

            // Text +=はURLごとにテキストボックス全体を作り直すため、StringBuilderに溜めて最後に1回だけセットする
            StringBuilder destText = new StringBuilder();

            int trimLineNum = Logic.GetTrimLine(textBox_TrimLineNum.Text);

            StringComparison comparison = (checkBox_OrdinalCase.IsChecked == true) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            StcDebug dbg = new StcDebug();
            dbg.IsDebugMode = IsDebug;

            String[] urls = textBox_SourceList.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            foreach (String url in urls)
            {
                destText.Append("◆").Append(url).Append(Environment.NewLine);

                String htmlSource = FetchHtmlSource(url);
                dbg.WriteDataInNewFile(htmlSource, "_1_source");

                String result = Logic.GetSearchString(htmlSource, textBox_SearchWord.Text, trimLineNum, comparison, checkBox_FirstWordOnly.IsChecked == true);
                dbg.WriteDataInNewFile(result, "_2_search");

                destText.Append(result);
            }
            textBox_DestList.Text = destText.ToString();

            CopyToClipboard(textBox_DestList.Text);
        }

        /// <summary>
        /// HTMLのソースを取得する(UTF-8として読む)。失敗したらメッセージを出して空文字を返す
        /// </summary>
        private String GetHtmlSource(String url)
        {
            String htmlSource = "";
            try
            {
                // .NET Framework 4.5向けのビルドは既定でTLS1.2を使わないため、今どきのHTTPSサイトにつながらない。
                // 既定の設定に1.2を足してから取得する
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (WebClient client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    htmlSource = client.DownloadString(url);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("ソース取得に失敗しました。" + Environment.NewLine + url);
            }

            return _util.ChangeNewLineCodeLf2Crlf(htmlSource);
        }

        private void comboBox_LoadSetting_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // SelectionChangedの時点ではTextがまだ古いので、SelectedItemから読む
            if (comboBox_LoadSetting.SelectedItem == null)
            {
                return;
            }
            SaveRestore.Load(Path.Combine(_userDataFolder, comboBox_LoadSetting.SelectedItem.ToString()));
        }

        private void button_SaveSetting_Click(object sender, RoutedEventArgs e)
        {
            WpfProfile.SaveProfileWithDialog(_fio, comboBox_LoadSetting, _profileExtensions, SaveRestore.Save, _userDataFolder, _settingFileName);
        }
    }
}
