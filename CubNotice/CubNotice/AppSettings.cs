using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace CubNotice
{
    /// <summary>
    /// アプリの設定(固定ヘッダ・テンプレート・取り込むページ番号)。
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// 取り込み時に各予定へ設定する固定ヘッダ。
        /// 文面はソースに持たず、設定ファイル(settings.json)だけで管理する。
        /// </summary>
        public string DefaultHeader { get; set; } = "";

        /// <summary>アナウンス文のテンプレート</summary>
        public string Template { get; set; } = AnnouncementFormatter.DefaultTemplate;

        /// <summary>取り込むPDFのページ番号(1始まり)</summary>
        public int PageNumber { get; set; } = 2;

        public static AppSettings Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new AppSettings();
            }
            return JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(filePath, Encoding.UTF8)) ?? new AppSettings();
        }

        public void Save(string filePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, JsonConvert.SerializeObject(this, Formatting.Indented), Encoding.UTF8);
        }
    }

    /// <summary>
    /// データの保存先。既定は%LOCALAPPDATA%\CubNotice。
    /// exeと同じフォルダにDataFolder.txtがあれば、その1行目のフォルダを使う(他ツールと同じ方式)。
    /// </summary>
    public static class DataLocation
    {
        public const string AppName = "CubNotice";

        public static string GetFolder()
        {
            string pointer = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataFolder.txt");
            if (File.Exists(pointer))
            {
                foreach (string line in File.ReadAllLines(pointer, Encoding.UTF8))
                {
                    if (line.Trim().Length > 0)
                    {
                        return Environment.ExpandEnvironmentVariables(line.Trim());
                    }
                }
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
        }

        public static string EventsFile
        {
            get { return Path.Combine(GetFolder(), "events.json"); }
        }

        public static string SettingsFile
        {
            get { return Path.Combine(GetFolder(), "settings.json"); }
        }
    }
}
