using Ccxc.Core.DbOrm;
using Ccxc.Core.Utils.ExtensionFunctions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ccxc_backend.DataModels
{
    public class temp_anno
    {
        [DbColumn(IsPrimaryKey = true, ColumnDescription = "题目ID")]
        public int pid { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "首杀时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime first_solve_time { get; set; }

        [DbColumn(ColumnDescription = "首杀组队GID")]
        public int first_solver_gid { get; set; }
    }

    public class TempAnno : MysqlClient<temp_anno>
    {
        public TempAnno(string connStr, RedisCacheConfig rcc) : base(connStr, rcc)
        {

        }
    }
}
