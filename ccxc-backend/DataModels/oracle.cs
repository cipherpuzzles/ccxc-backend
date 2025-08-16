using Ccxc.Core.DbOrm;
using Ccxc.Core.Utils.ExtensionFunctions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.DataModels
{
    public class oracle
    {
        [DbColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDescription = "人工提示ID")]
        public int oracle_id { get; set; }

        [DbColumn(ColumnDescription = "组ID", IndexGroupNameList = new string[] { "gid_pid" })]
        public int gid { get; set; }

        [DbColumn(ColumnDescription = "题目ID", IndexGroupNameList = new string[] { "gid_pid" })]
        public int pid { get; set; }

        [DbColumn(ColumnDescription = "问题内容（选手填写）", ColumnDataType = "TEXT", IsNullable = true)]
        public string question_content { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "问题内容最后编辑时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime update_time { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "提示创建时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime create_time { get; set; }

        [DbColumn(ColumnDescription = "是否已回复", DefaultValue = "0")]
        public byte is_reply { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "回复时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime reply_time { get; set; }

        [DbColumn(ColumnDescription = "回复内容", ColumnDataType = "TEXT", IsNullable = true)]
        public string reply_content { get; set; }

        [DbColumn(ColumnDescription = "扩展功能（自动打开已有提示用）", IsNullable = true)]
        public string extend_function { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(IsIgnore = true)]
        public DateTime unlock_time { get; set; }
    }

    public class Oracle : MysqlClient<oracle>
    {
        public Oracle(string connStr, RedisCacheConfig rcc) : base(connStr, rcc)
        {
            
        }
    }
}
