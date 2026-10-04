using System;
using System.Collections.ObjectModel;

namespace ListView
{
    public class Examination
    {
        public string Subject { get; set; }
        public int Point { get; set; }
        public string UserName { get; set; }
        public string ClassName { get; set; }
    }

    public class ExaminationList
    {
        // バインディングの指定先プロパティ
        public ObservableCollection<Examination> ExaminationResult { get; set; }

        // コンストラクタ(データ入力)
        public ExaminationList()
        {
            ExaminationResult = new ObservableCollection<Examination> {
                new Examination { Subject="Mathematics", Point=90, UserName="James", ClassName="A" },
                new Examination { Subject="National language", Point=50, UserName="Melinda", ClassName="B" },
                new Examination { Subject="Society", Point=70, UserName="Adam", ClassName="B" },
                new Examination { Subject="Mathematics", Point=80, UserName="Jemmy", ClassName="C" }
            };
        }
    }
}
