using ccxc_backend.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Admin
{
    public class QueryOracleAdminRequest
    {
        /// <summary>
        /// 组队GID
        /// </summary>
        public List<int> gid { get; set; }

        /// <summary>
        /// 题目ID
        /// </summary>
        public List<int> pid { get; set; }

        /// <summary>
        /// 已回复状态（0-全部 1-已回复 2-未回复）
        /// </summary>
        public int reply { get; set; }

        /// <summary>
        /// 时间排序（0-最新在前 1-最老在前）
        /// </summary>
        public int order { get; set; }

        public int page { get; set; }
    }

    public class OracleView : oracle
    {
        public OracleView(oracle t)
        {
            oracle_id = t.oracle_id;
            gid = t.gid;
            pid = t.pid;
            question_content = t.question_content;
            update_time = t.update_time;
            create_time = t.create_time;
            is_reply = t.is_reply;
            reply_time = t.reply_time;
            reply_content = t.reply_content;
            extend_function = t.extend_function;
            unlock_time = t.unlock_time;
        }

        public string group_name { get; set; }
        public int pgid { get; set; }
        public string puzzle_title { get; set; }
    }

    public class QueryOracleResponse : BasicResponse
    {
        public int page { get; set; }
        public int page_size { get; set; }
        public int total_count { get; set; }
        public List<OracleView> oracles { get; set; }
    }

    public class OracleReplyRequest
    {
        public int oracle_id { get; set; }
        public string reply_content { get; set; }
        public List<string> extend_function { get; set; }
    }
}
