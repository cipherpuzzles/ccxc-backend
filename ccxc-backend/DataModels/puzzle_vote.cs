using Ccxc.Core.DbOrm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.DataModels
{
    public class puzzle_vote
    {
        [DbColumn(IsPrimaryKey = true, ColumnDescription = "题目ID")]
        public int pid { get; set; }

        [DbColumn(IsPrimaryKey = true, ColumnDescription = "用户ID")]
        public int uid { get; set; }

        /// <summary>
        /// 投票类型，0-未投票 1-好评 2-差评
        /// </summary>
        [DbColumn(ColumnDescription = "投票类型（0-未投票 1-好评 2-差评）", DefaultValue = "0")]
        public int vote { get; set; }
    }

    public class PuzzleVote : MysqlClient<puzzle_vote>
    {
        public PuzzleVote(string connStr, RedisCacheConfig rcc) : base(connStr, rcc)
        {

        }
    }
}
