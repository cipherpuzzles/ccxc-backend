using Ccxc.Core.DbOrm;
using Ccxc.Core.Utils.ExtensionFunctions;
using Newtonsoft.Json;
using System;

namespace ccxc_backend.DataModels
{
    public class puzzle
    {
        [DbColumn(IsPrimaryKey = true, ColumnDescription = "题目ID")]
        public int pid { get; set; }

        [DbColumn(ColumnDescription = "题目组ID", IndexGroupNameList = new string[] { "index_pgid" })]
        public int pgid { get; set; }

        [DbColumn(ColumnDescription = "描述（显示在列表区域）", IsNullable = true)]
        public string desc { get;set; }

        /// <summary>
        /// 题目内容类型（0-图片 1-HTML 2-VUE SFC）
        /// </summary>
        [DbColumn(ColumnDescription = "内容类型（0-图片 1-HTML 2-VUE SFC 3-上传模块）", DefaultValue = "0")]
        public byte type { get; set; }

        [DbColumn(ColumnDescription = "标题", IsNullable = true)]
        public string title { get; set; }

        [DbColumn(ColumnDescription = "作者", IsNullable = true)]
        public string author { get; set; }

        [DbColumn(ColumnDescription = "附加数据", IsNullable = true)]
        public string extend_data { get; set; }

        [DbColumn(ColumnDescription = "题目描述", ColumnDataType = "TEXT", IsNullable = true)]
        public string content { get; set; }

        [DbColumn(ColumnDescription = "图片URL（type=0有效）", ColumnDataType = "TEXT", IsNullable = true)]
        public string image { get; set; }

        [DbColumn(ColumnDescription = "题目HTML（type=1，2有效）", ColumnDataType = "LONGTEXT", IsNullable = true)]
        public string html { get; set; }

        [DbColumn(ColumnDescription = "题目脚本（type=2有效）", ColumnDataType = "LONGTEXT", IsNullable = true)]
        public string script { get; set; }

        /// <summary>
        /// 答案类型（0-小题 1-组/区域Meta 2-PreFinalMeta 3-FinalMeta 4-不计分题目）
        /// 1- 完成该区域
        /// 2- 开放FinalMeta
        /// 3- 完赛，记录最终成绩
        /// </summary>
        [DbColumn(ColumnDescription = "答案类型（0-小题 1-组/区域Meta 2-PreFinalMeta 3-FinalMeta 4-不计分题目）")]
        public byte answer_type { get; set; }

        [DbColumn(ColumnDescription = "答案")]
        public string answer { get; set; }

        [DbColumn(ColumnDescription = "判题类型（0-标准判题函数 1-自定义判题函数）", IsNullable = true)]
        public int check_answer_type { get; set; }

        [DbColumn(ColumnDescription = "判题函数名", IsNullable = true)]
        public string check_answer_function { get; set; }

        [DbColumn(ColumnDescription = "初始允许尝试次数", DefaultValue = "20")]
        public int attempts_count { get; set; }

        [DbColumn(ColumnDescription = "隐藏题目跳转关键字", IsNullable = true)]
        public string jump_keyword { get; set; }

        [DbColumn(ColumnDescription = "附加内容（正解后显示）", ColumnDataType = "TEXT", IsNullable = true)]
        public string extend_content { get; set; }

        [DbColumn(ColumnDescription = "答案解析", ColumnDataType = "TEXT", IsNullable = true)]
        public string analysis { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "上次修改时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime dt_update { get; set; }
    }

    public class Puzzle : MysqlClient<puzzle>
    {
        public Puzzle(string connStr, RedisCacheConfig rcc) : base(connStr, rcc)
        {

        }
    }
}
