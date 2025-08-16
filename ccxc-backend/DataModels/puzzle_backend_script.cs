using Ccxc.Core.DbOrm;
using Ccxc.Core.Utils.ExtensionFunctions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ccxc_backend.DataModels
{
    public class puzzle_backend_script
    {
        [DbColumn(IsPrimaryKey = true, ColumnDescription = "脚本ID", IsIdentity = true)]
        public int psid { get; set; }

        [DbColumn(ColumnDescription = "索引名称", UniqueGroupNameList = new string[] { "psid_key" })]
        public string key { get; set; }

        [DbColumn(ColumnDescription = "脚本简介")]
        public string desc { get; set; }

        [DbColumn(ColumnDescription = "脚本内容", ColumnDataType = "LONGTEXT")]
        public string script { get; set; }

        [DbColumn(ColumnDescription = "脚本大小")]
        public int length { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "创建时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime dt_create { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "更新时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime dt_update { get; set; }
    }

    public class PuzzleBackendScript : MysqlClient<puzzle_backend_script>
    {
        public PuzzleBackendScript(string connStr, RedisCacheConfig rcc) : base(connStr, rcc)
        {

        }
    }
}
