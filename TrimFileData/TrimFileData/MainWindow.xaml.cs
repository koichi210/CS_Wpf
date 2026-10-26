using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StandardTemplate;
using StandardTemplate.Wpf;

namespace TrimFileData
{
    /// <summary>
    /// リファレンスファイル(テキスト)から、検索ワードを含む行だけを抜き出すツール。
    /// WinForms版(CS_Form\TrimFileData)のWPF移植。
    /// WinForms版が複数行欄のKeyDownで自前実装していたCtrl+Aの全選択は、WPFのTextBoxが標準で持っているので移植していない。
    /// </summary>
    public partial class MainWindow : Window
    {
        // データ保存先・設定ファイル名はWinForms版TrimFileDataと同じ(%LOCALAPPDATA%\TrimFileData\TrimFileData.json)。
        // 保存キーも同じにしてあるので、WinForms版で保存したJSONプロファイルをそのまま読める(逆も同じ)
        private const String _appName = "TrimFileData";
        private const String _settingFileName = "TrimFileData.json";

        // WinForms版は旧XML(*.xml)のプロファイルも一覧に出していたが、WpfSaveRestoreはXMLを読めないのでJSONだけにする
        // (旧XMLからJSONへの移行はWinForms版の起動時に済んでいる前提。Mailer/FileArranger等のWPF版と同じ方針)
        private static readonly String[] _profileExtensions = { "*.json" };

        // 「データ保存先を変更」で切り替わるので readonly にしない
        private String _userDataFolder;
        private readonly StcUtils _util = new StcUtils();
        private readonly StcFileInputOutput _fio = new StcFileInputOutput();
        internal WpfSaveRestore SaveRestore { get; } = new WpfSaveRestore();

        // 検索結果をクリップボードへ送る処理。テストで本物のクリップボードを書き換えないよう差し替えられるようにしてある
        internal Func<String, Boolean> CopyToClipboard { get; set; }

        public MainWindow() : this(UserDataLocation.GetUserDataFolder(_appName))
        {
        }

        // テスト用に、データ保存先を差し替えられるようにしてある(ユーザーの実データフォルダに触れないため)
        internal MainWindow(String dataFolder)
        {
            _userDataFolder = dataFolder;
            CopyToClipboard = _util.SetClipboardText;

            InitializeComponent();
            // リファレンスファイルパスに相対パスを書いた場合はexeの場所を基準にする(WinForms版のInitializeCommonSettingsと同じ)
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
            // 既定の設定ファイル(TrimFileData.json)を読む。無ければ既定値(空欄・チェック無し)
            SaveRestore.LoadOrDefault(Path.Combine(_userDataFolder, _settingFileName));
            // WinForms版と同じく既定の設定ファイルを選んだ状態にする(SelectionChangedでもう一度読み込まれる)
            WpfProfile.UpdateProfileList(comboBox_LoadSetting, _profileExtensions, _settingFileName, _userDataFolder);
        }

        private void RegisterSettingItems()
        {
            // キーはWinForms版(SaveRestore.RegisterItem)と同じにすること。
            // typo修正前の旧キー(Serch)で保存された設定ファイルも、legacyAttrValueで読み替えて読み込む
            SaveRestore.RegisterCtrl("Common", "textBox_ReferencePath", textBox_ReferencePath);
            SaveRestore.RegisterCtrl("Common", "textBox_SearchCommonWord", textBox_SearchCommonWord, legacyAttrValue: "textBox_SerchCommonWord");
            SaveRestore.RegisterCtrl("Common", "checkBox_FirstWordOnly", checkBox_FirstWordOnly);
            SaveRestore.RegisterCtrl("Common", "checkBox_OrdinalCase", checkBox_OrdinalCase);
            SaveRestore.RegisterCtrl("Common", "textBox_SearchWordList", textBox_SearchWordList, legacyAttrValue: "textBox_SerchWordList");
            SaveRestore.RegisterCtrl("Common", "textBox_SearchResultList", textBox_SearchResultList, legacyAttrValue: "textBox_SerchResultList");
        }

        private void button_Execute_Click(object sender, RoutedEventArgs e)
        {
            ExecuteSearch();
        }

        // 実行ボタンの処理本体。成功したらtrue(リファレンスファイルが開けなければメッセージを出してfalse)
        internal Boolean ExecuteSearch()
        {
            // 出力先をクリア
            textBox_SearchResultList.Text = "";

            String referData = _fio.LoadFile(textBox_ReferencePath.Text);
            if (referData == String.Empty)
            {
                MessageBox.Show("リファレンスファイルが開けません。" + Environment.NewLine + textBox_ReferencePath.Text);
                return false;
            }

            // 検索ワードをリストアップ
            // (LFだけの文字列を貼り付けた場合やLF改行のファイルでも行に分かれるよう、改行コードを問わず分ける)
            String[] searchWordLines = Logic.SplitLines(textBox_SearchWordList.Text);

            // リファレンスをリスト化
            String[] referLines = Logic.SplitLines(referData);

            // 検索結果をコントロールにセット
            textBox_SearchResultList.Text = Logic.GetSearchData(searchWordLines, referLines,
                checkBox_OrdinalCase.IsChecked == true, checkBox_FirstWordOnly.IsChecked == true, textBox_SearchCommonWord.Text);
            CopyToClipboard(textBox_SearchResultList.Text);
            return true;
        }

        private void textBox_ReferencePath_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _util.ExecutePath(textBox_ReferencePath.Text);
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
