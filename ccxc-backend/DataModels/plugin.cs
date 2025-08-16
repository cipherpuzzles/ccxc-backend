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
    public class plugin
    {
        [DbColumn(IsPrimaryKey = true, ColumnDescription = "插件名称")]
        public string plugin_name { get; set; }

        [DbColumn(ColumnDescription = "插件标题", IsNullable = true)]
        public string plugin_title { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "激活时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime active_time { get; set; }

        [DbColumn(ColumnDescription = "状态(0: 未激活, 1: 已激活)", DefaultValue = "0")]
        public int status { get; set; }

        [DbColumn(ColumnDescription = "安装位置", IsNullable = true)]
        public string install_path { get; set; }

        [DbColumn(ColumnDescription = "描述", IsNullable = true, ColumnDataType = "TEXT")]
        public string description { get; set; }

        [DbColumn(ColumnDescription = "版本", IsNullable = true)]
        public string version { get; set; }

        [DbColumn(ColumnDescription = "作者", IsNullable = true)]
        public string author { get; set; }

        [DbColumn(ColumnDescription = "入口程序集", IsNullable = true)]
        public string entry_assembly { get; set; }

        [DbColumn(ColumnDescription = "入口", IsNullable = true)]
        public string entry { get; set; }

        [DbColumn(ColumnDescription = "图标", IsNullable = true)]
        public string icon { get; set; }

        [DbColumn(ColumnDescription = "前端配置", IsNullable = true, ColumnDataType = "LONGTEXT")]
        public string frontend_components { get; set; }
    }

    public class Plugin : MysqlClient<plugin>
    {
        public Plugin(string connStr, RedisCacheConfig rcc) : base(connStr, rcc)
        {

        }
    }
}
