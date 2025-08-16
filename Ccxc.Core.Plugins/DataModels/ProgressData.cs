using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ccxc.Core.Plugins.DataModels
{
    public class ProgressData
    {
        /// <summary>
        /// 队伍ID
        /// </summary>
        public int gid { get; set; } = 0;

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

        /// <summary>
        /// 是否完成
        /// </summary>
        public bool IsFinish { get; set; } = false;

        /// <summary>
        /// 完成时间
        /// </summary>
        public DateTime FinishTime { get; set; } = DateTime.MinValue;
    }
}
