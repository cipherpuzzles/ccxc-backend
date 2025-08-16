using Ccxc.Core.DbOrm;
using Ccxc.Core.Utils.ExtensionFunctions;
using Newtonsoft.Json;
using System;

namespace ccxc_backend.DataModels
{
    public class answer_log
    {
        [DbColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDescription = "答案记录ID")]
        public int id { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "创建时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime create_time { get; set; }

        [DbColumn(ColumnDescription = "UID")]
        public int uid { get; set; }

        [DbColumn(ColumnDescription = "GID", IndexGroupNameList = new string[] { "index_gid_pid" })]
        public int gid { get; set; }

        [DbColumn(ColumnDescription = "题目ID", IndexGroupNameList = new string[] { "index_gid_pid" })]
        public int pid { get; set; }

        [DbColumn(ColumnDescription = "提交答案")]
        public string answer { get; set; }

        /// <summary>
        /// 答案状态（0-保留 1-正确 2-答案错误 3-答题次数用尽 4-里程碑 5-发生存档错误而未判定 6-该题目不可见而无法回答 7-解锁提示）
        /// </summary>
        [DbColumn(ColumnDescription = "答案状态（0-保留 1-正确 2-答案错误 3-答题次数用尽 4-里程碑 5-发生存档错误而未判定 6-该题目不可见而无法回答 7-解锁提示）", IndexGroupNameList = new string[] { "index_gid_pid" })]
        public byte status { get; set; }

        [DbColumn(ColumnDescription = "附加消息", IsNullable = true, ColumnDataType = "TEXT")]
        public string message { get; set; }
    }

    public class AnswerLog : MysqlClient<answer_log>
    {
        public AnswerLog(string connStr, RedisCacheConfig rcc) : base(connStr, rcc)
        {

        }
    }

}
