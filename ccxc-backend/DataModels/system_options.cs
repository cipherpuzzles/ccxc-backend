using Ccxc.Core.DbOrm;
using Ccxc.Core.Utils.ExtensionFunctions;
using Newtonsoft.Json;
using System;

namespace ccxc_backend.DataModels
{
    public class system_options
    {
        [DbColumn(IsPrimaryKey = true, ColumnDescription = "选项名称")]
        public string key { get; set; }

        [DbColumn(ColumnDescription = "选项值")]
        public string value { get; set; }
    }

    public class SystemOptions : MysqlClient<system_options>
    {
        public SystemOptions(string connStr, RedisCacheConfig rcc) : base(connStr, rcc)
        {

        }
    }
}
