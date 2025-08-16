using Ccxc.Core.DbOrm;
using Ccxc.Core.Utils.ExtensionFunctions;
using ccxc_backend.DataServices;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace ccxc_backend.DataModels
{
    public class announcement
    {
        [DbColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDescription = "公告ID")]
        public int aid { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "更新时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime update_time { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "创建时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime create_time { get; set; }

        [DbColumn(ColumnDescription = "公告内容", ColumnDataType = "TEXT", IsNullable = true)]
        public string content { get; set; }

        [DbColumn(ColumnDescription = "是否隐藏（0-不隐藏 1-隐藏）", DefaultValue = "0")]
        public int is_hide { get; set; }
    }

    public class Announcement : MysqlClient<announcement>
    {
        public Announcement(string connStr, RedisCacheConfig rcc) : base(connStr, rcc)
        {

        }
    }
}
