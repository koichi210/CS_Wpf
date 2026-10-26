using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
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

        // 「データ保存先を変更」で切り替わるので readonly にしない
        private String _userDataFolder;
        private readonly StcUtils _util = new StcUtils();
        private readonly StcFileInputOutput _fio = new StcFileInputOutput();
        internal WpfSaveRestore SaveRestore { get; } = new WpfSaveRestore();

        // デバッグモード中は、取得したHTMLと抽出結果をexeの場所へ連番ファイルで書き出す(StcDebug)。
        // 画面の余白をダブルクリックするたびに切り替わる(WinForms版と同じ隠し機能)
        internal Boolean IsDebug { get; private set; } = false;

        // URLからHTMLを取得する処理(UIスレッドの外で呼ばれる。失敗時は例外を投げる)と、結果をクリップボードへ送る処理、
        // メッセージを出す処理。テストで実サイトへ通信したり本物のクリップボードを書き換えたり
        // メッセージボックスで止まったりしないよう、差し替えられるようにしてある
        internal Func<String, String> FetchHtmlSource { get; set; }
        internal Func<String, Boolean> CopyToClipboard { get; set; }
        internal Action<String> ShowMessage { get; set; }

        public MainWindow() : this(UserDataLocation.GetUserDataFolder(_appName))
        {
        }

        // テスト用に、データ保存先を差し替えられるようにしてある(ユーザーの実データフォルダに触れないため)
        internal MainWindow(String dataFolder)
        {
            _userDataFolder = dataFolder;
            FetchHtmlSource = DownloadHtmlSource;
            CopyToClipboard = _util.SetClipboardText;
            ShowMessage = message => MessageBox.Show(this, message);

            InitializeComponent();
            // デバッグ出力の書き出し先をexeの場所にする(WinForms版のInitializeCommonSettingsと同じ)
            _util.SetCurrentDirectory();

            RegisterSettingItems();

            LoadProfilesFromDataFolder();

            // 保存先を変更したら、再起動しなくても新しい保存先のプロファイルを読み込み直す
            WpfDataFolderMenu.Attach(this, () => DataFolderMenu.ChangeDataFolder(_appName, _userDataFolder,
                (oldFolder, newFolder) => DataFolderMenu.MoveProfiles(oldFolder, newFolder, _appName),
                newFolder =>
                {
                    _userDataFolder = newFolder;
                    LoadProfilesFromDataFolder();
                }));
        }

        // 現在のデータ保存先から、既定の設定ファイルとプロファイル一覧を読み込む(起動時と保存先変更時に使う)
        private void LoadProfilesFromDataFolder()
        {
            // 既定の設定ファイル(TrimHtmlData.json)を読む。無ければ既定値(空欄・チェック無し)
            SaveRestore.LoadOrDefault(Path.Combine(_userDataFolder, _settingFileName));
            // WinForms版と同じく既定の設定ファイルを選んだ状態にする(SelectionChangedでもう一度読み込まれる)
            WpfProfile.UpdateProfileList(comboBox_LoadSetting, _profileExtensions, _settingFileName, _userDataFolder);
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

        // 取得中か(実行ボタンの二重押し防止)と、ウィンドウが閉じられたか。
        // 取得はUIスレッドの外で行うので、取得中に閉じられた後で結果が届いても、
        // 閉じた画面やクリップボードに触らないよう途中で打ち切るために見る
        internal Boolean IsFetching { get; private set; } = false;
        private Boolean _isClosed = false;

        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;
            base.OnClosed(e);
        }

        private async void button_Execute_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteTrimAsync();
        }

        // 実行ボタンの処理本体。URLごとに「◆URL」の見出しと抽出結果を並べて取得値欄に出し、クリップボードにもコピーする。
        // 以前はUIスレッドでそのまま取得していたため、取得が終わるまで画面が固まっていた。
        // 取得(FetchHtmlSource)だけを裏のスレッドに回し、結果の処理はURLごとにUIスレッドへ戻ってから行う
        // (失敗時のメッセージをURLの順に1件ずつ出す、以前の挙動を保つため)
        internal async Task ExecuteTrimAsync()
        {
            if (IsFetching)
            {
                return;
            }

            int trimLineNum;
            if (!Logic.TryGetTrimLine(textBox_TrimLineNum.Text, out trimLineNum))
            {
                ShowMessage(Logic.TrimLineNumErrorMessage);
                textBox_TrimLineNum.Focus();
                textBox_TrimLineNum.SelectAll();
                return;
            }

            // 出力先をクリア
            textBox_DestList.Text = "";

            // Text +=はURLごとにテキストボックス全体を作り直すため、StringBuilderに溜めて最後に1回だけセットする
            StringBuilder destText = new StringBuilder();

            // 取得中も画面は操作できる(設定の読み込みで各欄が書き換わることもある)ので、
            // 処理に使う値は開始時点でまとめて読み取っておく
            String searchWord = textBox_SearchWord.Text;
            StringComparison comparison = (checkBox_OrdinalCase.IsChecked == true) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            Boolean firstWordOnly = checkBox_FirstWordOnly.IsChecked == true;

            StcDebug dbg = new StcDebug();
            dbg.IsDebugMode = IsDebug;

            String[] urls = Logic.SplitLines(textBox_SourceList.Text);
            Func<String, String> fetchHtmlSource = FetchHtmlSource;

            SetFetching(true);
            for (int i = 0; i < urls.Length; i++)
            {
                String url = urls[i];
                destText.Append("◆").Append(url).Append(Environment.NewLine);
                Title = _appName + " - 取得中 (" + (i + 1) + "/" + urls.Length + ")";

                String htmlSource = "";
                Boolean isFailed = false;
                try
                {
                    htmlSource = await Task.Run(() => fetchHtmlSource(url));
                }
                catch (Exception)
                {
                    isFailed = true;
                }

                if (_isClosed)
                {
                    return;
                }

                if (isFailed)
                {
                    ShowMessage("ソース取得に失敗しました。" + Environment.NewLine + url);

                    // メッセージを出している間に閉じられることもある
                    if (_isClosed)
                    {
                        return;
                    }
                }

                htmlSource = _util.ChangeNewLineCodeLf2Crlf(htmlSource ?? "");
                dbg.WriteDataInNewFile(htmlSource, "_1_source");

                String result = Logic.GetSearchString(htmlSource, searchWord, trimLineNum, comparison, firstWordOnly);
                dbg.WriteDataInNewFile(result, "_2_search");

                destText.Append(result);
            }
            textBox_DestList.Text = destText.ToString();
            SetFetching(false);

            CopyToClipboard(textBox_DestList.Text);
        }

        // 取得中は実行ボタンを押せなくし、ボタンとタイトルで取得中であることを示す
        private void SetFetching(Boolean isFetching)
        {
            IsFetching = isFetching;
            button_Execute.IsEnabled = !isFetching;
            button_Execute.Content = isFetching ? "取得中..." : "実行";
            if (!isFetching)
            {
                Title = _appName;
            }
        }

        /// <summary>
        /// HTMLのソースを取得する(UTF-8として読む)。UIスレッドの外で呼ばれる。
        /// 失敗時は例外をそのまま投げ、呼び出し元(ExecuteTrimAsync)がメッセージを出す
        /// </summary>
        private static String DownloadHtmlSource(String url)
        {
            // .NET Framework 4.5向けのビルドは既定でTLS1.2を使わないため、今どきのHTTPSサイトにつながらない。
            // 既定の設定に1.2を足してから取得する
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                return client.DownloadString(url);
            }
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
