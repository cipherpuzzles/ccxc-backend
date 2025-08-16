using Ccxc.Core.DbOrm;
using Ccxc.Core.Utils.ExtensionFunctions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ccxc_backend.DataModels
{
    public class progress
    {
        [DbColumn(IsPrimaryKey = true, ColumnDescription = "组ID")]
        public int gid { get; set; }

        [DbColumn(ColumnDescription = "存档数据", IsJson = true, ColumnDataType = "JSON")]
        public SaveData data { get; set; } = new SaveData();

        [DbColumn(ColumnDescription = "得分（排序依据，不展示）")]
        public double score { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "更新时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime update_time { get; set; }

        [DbColumn(ColumnDescription = "是否完赛（0-未完赛 1-完赛）")]
        public byte is_finish { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "完成时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime finish_time { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "完成时间temp", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime finish_time_temp { get; set; }

        [DbColumn(ColumnDescription = "能量点")]
        public int power_point { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "能量点更新时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime power_point_update_time { get; set; }
    }

    public class SaveData
    {
        /// <summary>
        /// 已完成的题目（pid）
        /// </summary>
        public HashSet<int> FinishedProblems { get; set; } = new HashSet<int>();

        /// <summary>
        /// 已解锁的小题（pid）
        /// </summary>
        public HashSet<int> UnlockedProblems { get; set; } = new HashSet<int>();

        /// <summary>
        /// 已完成的分区（pgid）
        /// </summary>
        public HashSet<int> FinishedGroups { get; set; } = new HashSet<int>();

        /// <summary>
        /// 已解锁的分区（pgid）
        /// </summary>
        public HashSet<int> UnlockedGroups { get; set; } = new HashSet<int>();

        /// <summary>
        /// 题目解锁时间（pid -> 解锁时间）
        /// </summary>
        public Dictionary<int, DateTime> ProblemUnlockTime { get; set; } = new Dictionary<int, DateTime>();

        /// <summary>
        /// 各题目的答案提交次数（pid -> 次数）
        /// </summary>
        public Dictionary<int, int> ProblemAnswerSubmissionsCount { get; set; } = new Dictionary<int, int>();

        /// <summary>
        /// 已购买的额外题目回答次数（pid -> 次数）
        /// </summary>
        public Dictionary<int, int> AdditionalProblemAttemptsCount { get; set; } = new Dictionary<int, int>();

        /// <summary>
        /// 已兑换过的提示（pid -> (提示id)）
        /// </summary>
        public Dictionary<int, HashSet<int>> OpenedHints { get; set; } = new Dictionary<int, HashSet<int>>();

        /// <summary>
        /// 题目当前状态（pid -> (状态名 -> 状态值)）
        /// </summary>
        public Dictionary<int, Dictionary<string, string>> ProblemStatus { get; set; } = new Dictionary<int, Dictionary<string, string>>();
    }


    public class Progress : MysqlClient<progress>
    {
        public Progress(string connStr, RedisCacheConfig rcc) : base(connStr, rcc)
        {

        }

        public static progress Guest
        {
            get
            {
                var data = new SaveData();
                for (var i = 1; i <= 55; i ++)
                {
                    data.FinishedProblems.Add(i);
                    data.UnlockedProblems.Add(i);
                    data.ProblemUnlockTime.Add(i, DateTime.Now);
                    data.ProblemAnswerSubmissionsCount.Add(i, 0);
                    data.OpenedHints.Add(i, new HashSet<int> { -1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 100, 101, 102, 103, 104, 105, 106, 107 });
                }

                return new progress
                {
                    gid = -1,
                    data = data,
                    score = 0,
                    update_time = DateTime.Now,
                    is_finish = 1,
                    finish_time = DateTime.Now,
                    finish_time_temp = DateTime.Now,
                    power_point = 99999999,
                    power_point_update_time = DateTime.Now
                };
            }
        }
    }
}
