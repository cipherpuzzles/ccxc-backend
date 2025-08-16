using Ccxc.Core.DbOrm;
using System;
using System.Collections.Generic;
using System.Text;

namespace ccxc_backend.DataModels
{
    public class puzzle_tips
    {
        [DbColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDescription = "提示ID")]
        public int ptid { get; set; }

        [DbColumn(ColumnDescription = "提示顺序")]
        public int order {get; set; }

        [DbColumn(ColumnDescription = "所属题目ID", IndexGroupNameList = new string[] { "index_pid" })]
        public int pid { get; set; }

        [DbColumn(ColumnDescription = "标题")]
        public string title { get; set; }

        [DbColumn(ColumnDescription = "内容", ColumnDataType = "TEXT", IsNullable = true)]
        public string content { get; set; }

        [DbColumn(ColumnDescription = "备注", IsNullable = true)]
        public string desc { get; set; }

        [DbColumn(ColumnDescription = "消耗能量点")]
        public int point_cost { get; set; }

        [DbColumn(ColumnDescription = "解锁延迟时间（单位：分钟）")]
        public double unlock_delay { get; set; }
    }

    public class PuzzleTips : MysqlClient<puzzle_tips>
    {
        public PuzzleTips(string connStr, RedisCacheConfig rcc) : base(connStr, rcc)
        {

        }
    }
}
