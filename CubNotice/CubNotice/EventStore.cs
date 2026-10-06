using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace CubNotice
{
    /// <summary>取り込みの件数。</summary>
    public class ImportCounts
    {
        public int Added { get; set; }
        public int Updated { get; set; }

        /// <summary>詳細ありの予定が既にあるため取り込まなかった「予定のみ」の件数</summary>
        public int Skipped { get; set; }
    }

    /// <summary>
    /// 活動予定の一覧(データ追加・取得・破棄の3フェーズ)を管理し、JSONファイルに保存する。
    /// </summary>
    public class EventStore
    {
        /// <summary>案内を作るときに選べる予定の数(次回・次の次)</summary>
        public const int UpcomingChoiceCount = 2;

        private readonly string filePath;

        public EventStore(string filePath)
        {
            this.filePath = filePath;
        }

        public List<CubEvent> Events { get; private set; } = new List<CubEvent>();

        public void Load()
        {
            Events = File.Exists(filePath)
                ? JsonConvert.DeserializeObject<List<CubEvent>>(File.ReadAllText(filePath, Encoding.UTF8)) ?? new List<CubEvent>()
                : new List<CubEvent>();
            Sort();
        }

        public void Save()
        {
            Sort();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, JsonConvert.SerializeObject(Events, Formatting.Indented), Encoding.UTF8);
        }

        /// <summary>
        /// データ追加フェーズ: 予定を追加する。同じ日付・同じタイトルの予定は上書きする。
        /// 「予定のみ」(☆1行だけ)の予定は、後から同じ日の詳細ありの予定が来たら置き換わる。
        /// 逆に詳細ありの予定がある日に「予定のみ」が来ても上書きしない。
        /// </summary>
        public ImportCounts AddOrUpdate(IEnumerable<CubEvent> events)
        {
            ImportCounts counts = new ImportCounts();
            foreach (CubEvent e in events)
            {
                if (e.HasDetail)
                {
                    // タイトルは号によって書き方が変わる(「…（午前午後の活動）」など)ので、日付だけで置き換える
                    Events.RemoveAll(x => x.Date.Date == e.Date.Date && !x.HasDetail);
                }
                else if (Events.Any(x => x.Date.Date == e.Date.Date && x.HasDetail))
                {
                    counts.Skipped++;
                    continue;
                }

                int index = Events.FindIndex(x => x.Date.Date == e.Date.Date && x.Title == e.Title);
                if (index >= 0)
                {
                    Events[index] = e;
                    counts.Updated++;
                }
                else
                {
                    Events.Add(e);
                    counts.Added++;
                }
            }
            Sort();
            return counts;
        }

        /// <summary>
        /// データ破棄フェーズ: 基準日より前(当日は残す)の予定を削除する。
        /// </summary>
        /// <returns>削除した予定</returns>
        public List<CubEvent> RemovePast(DateTime today)
        {
            List<CubEvent> removed = Events.Where(e => e.Date.Date < today.Date).ToList();
            Events.RemoveAll(e => e.Date.Date < today.Date);
            return removed;
        }

        /// <summary>
        /// データ取得フェーズ: 基準日以降でいちばん近い予定を返す(当日の予定も「次回」に含める)。無ければnull。
        /// </summary>
        public CubEvent FindNext(DateTime today)
        {
            return FindUpcoming(today, 1).FirstOrDefault();
        }

        /// <summary>
        /// 基準日以降の予定を近い順に最大count件返す(次回・次の次…)。
        /// </summary>
        public List<CubEvent> FindUpcoming(DateTime today, int count)
        {
            return Events.Where(e => e.Date.Date >= today.Date).OrderBy(e => e.Date).Take(count).ToList();
        }

        private void Sort()
        {
            Events = Events.OrderBy(e => e.Date).ToList();
        }
    }
}
