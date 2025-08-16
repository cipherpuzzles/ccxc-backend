using Ccxc.Core.DbOrm;
using Ccxc.Core.Utils.ExtensionFunctions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.DataModels
{
    public class myoph_log
    {
        [DbColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDescription = "记录ID")]
        public int id { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "记录时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime create_time { get; set; }

        [DbColumn(ColumnDescription = "GID")]
        public int gid { get; set; }

        [DbColumn(ColumnDescription = "UID")]
        public int uid { get; set; }

        [DbColumn(ColumnDescription = "用户输入", ColumnDataType = "TEXT", IsNullable = true)]
        public string input { get; set; }

        [DbColumn(ColumnDescription = "是否成功")]
        public int success { get; set; }
    }

    public class MyophLog : MysqlClient<myoph_log>
    {
        public MyophLog(string connStr, RedisCacheConfig rcc) : base(connStr, rcc)
        {

        }
    }
}
