using System;

namespace SQLite.Objects
{
    class Customer
    {
        /// <summary>
        ///  using SQLite
        ///  PrimaryKey : 重複しない値
        ///  AutoIncrement : 1から順にナンバリング
        /// </summary>
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// 名前
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 電話番号
        /// </summary>
        public string Phone { get; set; }
    }
}
