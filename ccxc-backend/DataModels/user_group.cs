using Ccxc.Core.DbOrm;
using Ccxc.Core.Utils.ExtensionFunctions;
using Newtonsoft.Json;
using System;

namespace ccxc_backend.DataModels
{
    public class user_group
    {
        [DbColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDescription = "组ID")]
        public int gid { get; set; }

        [DbColumn(ColumnDescription = "组名", IndexGroupNameList = new string[] { "index_name" })]
        public string groupname { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "更新时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime update_time { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "创建时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime create_time { get; set; }

        [DbColumn(ColumnDescription = "简介", ColumnDataType = "TEXT", IsNullable = true)]
        public string profile { get; set; }

        [DbColumn(ColumnDescription = "是否隐藏（0-显示 1-隐藏）", DefaultValue = "0")]
        public int is_hide { get; set; }
    }

    public class UserGroup : MysqlClient<user_group>
    {
        public UserGroup(string connStr, RedisCacheConfig rcc) : base(connStr, rcc)
        {

        }
    }
}
