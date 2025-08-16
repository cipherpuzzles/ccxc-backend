using Ccxc.Core.DbOrm;
using System;
using System.Collections.Generic;
using System.Text;

namespace ccxc_backend.DataModels
{
    public class additional_answer
    {
        [DbColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDescription = "附加答案ID")]
        public int aaid { get; set; }

        [DbColumn(ColumnDescription = "题目ID", IndexGroupNameList = new string[] { "index_pid" })]
        public int pid { get; set; }

        [DbColumn(ColumnDescription = "附加答案")]
        public string answer { get; set; }

        [DbColumn(ColumnDescription = "附加消息", ColumnDataType = "TEXT", IsNullable = true)]
        public string message { get; set; }

        [DbColumn(ColumnDescription = "操作信息", IsNullable = true)]
        public string extra { get; set; }

        [DbColumn(ColumnDescription = "不计数(0-计数 1-不计数)", DefaultValue = "0")]
        public int not_count { get; set; }
    }

    public class AdditionalAnswer : MysqlClient<additional_answer>
    {
        public AdditionalAnswer(string connStr, RedisCacheConfig rcc) : base(connStr, rcc)
        {

        }
    }
}
