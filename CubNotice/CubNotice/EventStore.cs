using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace CubNotice
{
    /// <summary>
    /// 活動予定の一覧(データ追加・取得・破棄の3フェーズ)を管理し、JSONファイルに保存する。
    /// </summary>
    public class EventStore
    {
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
        /// </summary>
        /// <returns>(追加件数, 上書き件数)</returns>
        public Tuple<int, int> AddOrUpdate(IEnumerable<CubEvent> events)
        {
            int added = 0;
            int updated = 0;
            foreach (CubEvent e in events)
            {
                int index = Events.FindIndex(x => x.Date.Date == e.Date.Date && x.Title == e.Title);
                if (index >= 0)
                {
                    Events[index] = e;
                    updated++;
                }
                else
                {
                    Events.Add(e);
                    added++;
                }
            }
            Sort();
            return Tuple.Create(added, updated);
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
            return Events.Where(e => e.Date.Date >= today.Date).OrderBy(e => e.Date).FirstOrDefault();
        }

        private void Sort()
        {
            Events = Events.OrderBy(e => e.Date).ToList();
        }
    }
}
