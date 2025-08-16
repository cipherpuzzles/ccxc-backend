using ccxc_backend.DataServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Functions
{
    public static class RedisNumberCenter
    {
        //修改下面的定义部分，以适配提取数据的需要。
        //请记得同时修改Admin/DynamicNumericalController.cs中对应方法，以保证后台管理界面的正常运行。


        /// <summary>
        /// 初始能量点
        /// </summary>
        public static int InitialPowerPoint
        {
            get
            {
                return GetInt("initial_power_point").GetAwaiter().GetResult();
            }
            set
            {
                SetInt("initial_power_point", value).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// 能量点增长速率
        /// </summary>
        public static int PowerIncreaseRate
        {
            get
            {
                return GetInt("power_increase_rate").GetAwaiter().GetResult();
            }
            set
            {
                SetInt("power_increase_rate", value).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// 增加一倍尝试消耗次数的能量点价格
        /// </summary>
        public static int AddAttemptsCountCost
        {
            get
            {
                return GetInt("add_attempts_count_cost").GetAwaiter().GetResult();
            }
            set
            {
                SetInt("add_attempts_count_cost", value).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// 提示功能解锁时间（在解锁题目后的 x 分钟后才能使用提示功能）
        /// </summary>
        public static int UnlockTipFunctionAfter
        {
            get
            {
                return GetInt("unlock_tip_function_after").GetAwaiter().GetResult();
            }
            set
            {
                SetInt("unlock_tip_function_after", value).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// 人工提示反馈延迟时间（分钟）人工提示在发出后至少等待 x 分钟后才能查看反馈内容。
        /// </summary>
        public static int ManualTipReplyDelay
        {
            get
            {
                return GetInt("manual_tip_reply_delay").GetAwaiter().GetResult();
            }
            set
            {
                SetInt("manual_tip_reply_delay", value).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// 默认人工提示消耗能量点
        /// </summary>
        public static int DefaultOracleCost
        {
            get
            {
                return GetInt("default_oracle_cost").GetAwaiter().GetResult();
            }
            set
            {
                SetInt("default_oracle_cost", value).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// 初始解锁分区数量
        /// </summary>
        public static int InitialGroupCount
        {
            get
            {
                return GetInt("initial_group_count").GetAwaiter().GetResult();
            }
            set
            {
                SetInt("initial_group_count", value).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// 分区解锁时，同时解锁的题目数量（逗号分隔）
        /// </summary>
        public static string _firstUnlockEachGroupCount
        {
            get
            {
                return GetString("first_unlock_each_group_count").GetAwaiter().GetResult();
            }
            set
            {
                SetString("first_unlock_each_group_count", value).GetAwaiter().GetResult();
            }
        }

        public static int GetFirstUnlockInGroup(int pgid)
        {
            var index = pgid - 1;

            var counts = _firstUnlockEachGroupCount.Split(',');
            if (index >= 0 && index < counts.Length)
            {
                _ = int.TryParse(counts[index], out var count);
                return count;
            }

            return 0;
        }

        public static string _unlockMetaEachGroupCount
        {
            get
            {
                return GetString("unlock_meta_each_group_count").GetAwaiter().GetResult();
            }
            set
            {
                SetString("unlock_meta_each_group_count", value).GetAwaiter().GetResult();
            }
        }

        public static int GetUnlockMetaInGroup(int pgid)
        {
            var index = pgid - 1;
            var counts = _unlockMetaEachGroupCount.Split(',');
            if (index >= 0 && index < counts.Length)
            {
                _ = int.TryParse(counts[index], out var count);
                return count;
            }
            return 0;
        }

        public static string _unlockNextGroupCount
        {
            get
            {
                return GetString("unlock_next_group_count").GetAwaiter().GetResult();
            }
            set
            {
                SetString("unlock_next_group_count", value).GetAwaiter().GetResult();
            }
        }

        public static int GetUnlockNextGroupCountInGroup(int pgid)
        {
            var index = pgid - 1;
            var counts = _unlockNextGroupCount.Split(',');
            if (index >= 0 && index < counts.Length)
            {
                _ = int.TryParse(counts[index], out var count);
                return count;
            }
            return 0;
        }

        /// <summary>
        /// 在完成分区1后，自动解锁到哪个分区
        /// </summary>
        public static int MaxAutoUnlockGroup
        {
            get
            {
                return GetInt("max_auto_unlock_group").GetAwaiter().GetResult();
            }
            set
            {
                SetInt("max_auto_unlock_group", value).GetAwaiter().GetResult();
            }
        }


        #region Redis存储部分，你不应该修改这些内容
        private const string RedisPrefix = "ccxc:runtimecache:";
        private static async Task SetInt(string key, int value)
        {
            var cache = DbFactory.GetCache();
            var rkey = RedisPrefix + key;
            await cache.PutString(rkey, value.ToString());
        }

        private static async Task<int> GetInt(string key)
        {
            var cache = DbFactory.GetCache();
            var rkey = RedisPrefix + key;
            var intString = await cache.GetString(rkey);
            _ = int.TryParse(intString, out var value);
            return value;
        }

        private static async Task SetString(string key, string value)
        {
            var cache = DbFactory.GetCache();
            var rkey = RedisPrefix + key;
            await cache.PutString(rkey, value);
        }

        private static async Task<string> GetString(string key)
        {
            var cache = DbFactory.GetCache();
            var rkey = RedisPrefix + key;
            return await cache.GetString(rkey);
        }
        #endregion
    }
}
