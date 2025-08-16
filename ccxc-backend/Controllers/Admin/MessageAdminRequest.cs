using System;
using System.Collections.Generic;
using System.Text;

namespace ccxc_backend.Controllers.Admin
{
    public class QueryMessageAdminRequest
    {
        /// <summary>
        /// 组队GID（0-全部）
        /// </summary>
        public int gid { get; set; }

        /// <summary>
        /// 方向（0-全部 1-发出的消息 2-收到的消息）
        /// </summary>
        public int direction { get; set; }

        /// <summary>
        /// 已读状态（0-全部 1-已读 2-未读）
        /// </summary>
        public int read { get; set; }

        /// <summary>
        /// 时间排序（0-最新在前 1-最老在前）
        /// </summary>
        public int order { get; set; }

        public int page { get; set; }
    }

    public class MessageAdminRequest
    {
        public int mid { get; set; }
        public int type { get; set; } = 1;
    }

    public class AddMessageRequest
    {
        public int gid { get; set; }
        public string content { get; set; }
    }
}
