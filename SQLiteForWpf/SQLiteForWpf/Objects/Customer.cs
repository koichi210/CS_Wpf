using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace SQLiteForWpf.Objects
{
    class Customer
    {
        /// <summary>
        ///  using SQLite
        ///  PrimaryKey : 重複しない値
        ///  AutoIncrement : 1から順にナンバリング
        /// </summary>
        [PrimaryKey, AutoIncrement]
        public Int32 Id { get; set; }

        /// <summary>
        /// 名前
        /// </summary>
        public String Name { get; set; }

        /// <summary>
        /// 電話番号
        /// </summary>
        public String Phone { get; set; }
    }
}
