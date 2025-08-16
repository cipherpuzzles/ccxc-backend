using Ccxc.Core.DbOrm;
using Ccxc.Core.Utils.ExtensionFunctions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ccxc_backend.DataModels
{
    public class puzzle_article
    {
        [DbColumn(IsPrimaryKey = true, ColumnDescription = "文章ID", IsIdentity = true)]
        public int paid { get; set; }

        [DbColumn(ColumnDescription = "索引名称", UniqueGroupNameList = new string[] { "paid_key" })]
        public string key { get; set; }

        [DbColumn(ColumnDescription = "文章标题")]
        public string title { get; set; }

        [DbColumn(ColumnDescription = "文章内容", ColumnDataType = "TEXT")]
        public string content { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "发表时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime dt_create { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "更新时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime dt_update { get; set; }

        [DbColumn(ColumnDescription = "是否隐藏", DefaultValue = "0", ColumnDataType = "TINYINT")]
        public int is_hide { get; set; }
    }

    public class PuzzleArticle : MysqlClient<puzzle_article>
    {
        public PuzzleArticle(string connStr, RedisCacheConfig rcc) : base(connStr, rcc)
        {

        }
    }
}
