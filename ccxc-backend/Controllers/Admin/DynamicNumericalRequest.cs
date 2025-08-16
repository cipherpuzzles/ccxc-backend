using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Admin
{
    public class DynamicNumerical
    {
        /// <summary>
        /// 初始能量点
        /// </summary>
        public int initial_power_point { get; set; }

        /// <summary>
        /// 能量增加速度（每分钟）（更新需要通过单独API）
        /// </summary>
        public int power_increase_rate { get; set; }

        /// <summary>
        /// 增加一倍尝试次数消耗的能量点
        /// </summary>
        public int add_attempts_count_cost { get; set; }

        /// <summary>
        /// 提示功能解锁时间（单位：分钟）
        /// </summary>
        public int unlock_tip_function_after { get; set; }

        /// <summary>
        /// 人工提示回复延迟时间（单位：分钟）
        /// </summary>
        public int manual_tip_reply_delay { get; set; }

        /// <summary>
        /// 默认人工提示消耗能量点
        /// </summary>
        public int default_oracle_cost { get; set; }

        /// <summary>
        /// 初始解锁的分区数量
        /// </summary>
        public int initial_group_count { get; set; }

        public string first_unlock_each_group_count { get; set; }
        public string unlock_meta_each_group_count { get; set; }
        public string unlock_next_group_count { get; set; }

        public int max_auto_unlock_group { get; set; }
    }

    public class GetDynamicNumericalResponse : BasicResponse
    {
        public DynamicNumerical data { get; set; }
    }

    public class UnlockNextPuzzleForallRequest
    {
        public int pgid { get; set; }
        public int unlock_count { get; set; }
    }
}
