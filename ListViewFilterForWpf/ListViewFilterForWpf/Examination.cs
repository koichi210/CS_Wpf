using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace ListViewFilterForWpf
{
    public class Examination
    {
        public Int32 Id { get; set; }
        public String Subject { get; set; }
        public Int32 Point { get; set; }
        public String UserName { get; set; }
        public String ClassName { get; set; }

        public override String ToString()
        {
            return "${Id} - ${Subject} - ${Point} - ${UserName} - ${ClassName}";
        }
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

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
 
        public override string ToString()
        {
            return "${Id} - ${Name} - ${Phone}";
        }
    }
}
