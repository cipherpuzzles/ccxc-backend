using System;
using System.Collections.Generic;
using System.Text;

namespace ccxc_backend.Controllers.Game
{
    public class CheckAnswerRequest
    {
        public int pid { get; set; }
        public string answer { get; set; }
    }

    public class AnswerResponse : BasicResponse
    {
        /// <summary>
        /// 答案状态（0-保留 1-正确 2-答案错误 3-答题次数用尽 4-里程碑 5-发生存档错误而未判定 6-该题目不可见而无法回答 7-解锁提示）
        /// </summary>
        public int answer_status { get; set; }
        /// <summary>
        /// 0-什么都不做 1-跳转到指定路径（指定路径存入location中） 16-重新载入页面
        /// </summary>
        public int extend_flag { get; set; }
    }
}
