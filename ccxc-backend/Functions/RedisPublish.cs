using ccxc_backend.DataServices;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Functions
{
    public class RedisPushMsg
    {
        public int uid { get; set; }
        public int gid { get; set; }
        public string title { get; set; }
        public string content { get; set; }
        public string type { get; set; }

        /// <summary>
        /// 0-所有用户均需显示（解锁提示等） 1-和uid相同的用户隐藏（回答信息等）
        /// </summary>
        public int show_type { get; set; }
    }


    public static class RedisPublish
    {
        public static async Task Publish(RedisPushMsg message)
        {
            //redis用于推送
            var cache = DbFactory.GetCache();
            using var redis = cache.GetNotDisposedClient();
            var redisSub = await redis.GetSubscriber();
            var redisSubKey = "ccxc:notify";

            var pushString = JsonConvert.SerializeObject(message);
            await redisSub.PublishAsync(redisSubKey, pushString);

            // using语句会自动调用Dispose()
        }
    }
}
