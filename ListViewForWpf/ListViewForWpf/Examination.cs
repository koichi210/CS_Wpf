using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace ListViewForWpf
{
    public class Examination
    {
        public String Subject { get; set; }
        public Int32 Point { get; set; }
        public String UserName { get; set; }
        public String ClassName { get; set; }
    }

    public class ExaminationList
    {
        // バインディングの指定先プロパティ
        public ObservableCollection<Examination> ExaminationResult { get; set; }

        // コンストラクタ(データ入力)
        public ExaminationList()
        {
            ExaminationResult = new ObservableCollection<Examination> {
                new Examination { Subject="Mathematics", Point=90, UserName="Jeams", ClassName="A" },
                new Examination { Subject="National language", Point=50, UserName="Melinda", ClassName="B" },
                new Examination { Subject="Society", Point=70, UserName="Adam", ClassName="B" },
                new Examination { Subject="Mathematics", Point=80, UserName="Jemmy", ClassName="C" }
            };
        }
    }
}
