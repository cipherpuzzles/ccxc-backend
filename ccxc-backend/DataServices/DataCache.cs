using Ccxc.Core.DbOrm;
using Ccxc.Core.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.DataServices
{
    public class DataCache : IDataCache
    {
        private readonly string RedisConnStr;
        private readonly int RedisDatabase;

        public DataCache(string redisConnStr, int redisDatabase = 0)
        {
            RedisConnStr = redisConnStr;
            RedisDatabase = redisDatabase;
        }

        public RedisClient GetNotDisposedClient()
        {
            // 返回已初始化的客户端，调用者必须手动释放
            return RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
        }

        public async Task Put(string key, object value, long timeoutMilliseconds = -1)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            await redis.PutObject(key, value, timeoutMilliseconds);
        }

        public async Task PutString(string key, string value, long timeoutMilliseconds = -1)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            await redis.PutString(key, value, timeoutMilliseconds);
        }

        public async Task<T> Get<T>(string key)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            return await redis.GetObject<T>(key);
        }

        public async Task<string> GetString(string key)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            return await redis.GetString(key);
        }

        public async Task PutAll(string key, IDictionary<string, object> values, long timeoutMilliseconds = -1)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            await redis.PutHash(key, values, timeoutMilliseconds);
        }

        public async Task<T> GetFromPk<T>(string key, string pk)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            return await redis.GetHash<T>(key, pk);
        }

        public async Task<List<T>> GetAll<T>(string key)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            return await redis.GetHashAll<T>(key);
        }

        public async Task<List<(string key, T value)>> GetHashKeys<T>(string key)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            return await redis.GetHashKeys<T>(key);
        }

        public async Task PutList(string key, IList<object> list, long timeout)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            await redis.PutList(key, list, timeout);
        }

        public async Task<List<T>> GetList<T>(string key, int start, int end, long updateTimeout)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            return await redis.GetList<T>(key, start, end, updateTimeout);
        }

        public async Task<long> GetListLength(string key)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            return await redis.GetListLength(key);
        }

        public async Task<long> GetHashLength(string key)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            return await redis.GetHashLength(key);
        }

        public async Task Delete(string key)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            await redis.Delete(key);
        }

        public async Task Delete(string key, string hkey)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            await redis.Delete(key, hkey);
        }

        public List<string> FindKeys(string keyPattern)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            return redis.FindKeys(keyPattern).GetAwaiter().GetResult();
        }

        public async Task<List<(string key, T value)>> SearchHashKey<T>(string key, string pattern)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            return await redis.SearchHashKey<T>(key, pattern);
        }

        public async Task<List<(string key, T value)>> SearchHashValue<T>(string key, string pattern)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            return await redis.SearchHashValue<T>(key, pattern);
        }

        public async Task<int> RateLimiter(string key, int max_permits, long current_time, int rate_per_day, int request_permits)
        {
            using var redis = RedisClient.CreateInitialized(RedisConnStr, RedisDatabase);
            return await redis.RateLimiter(key, max_permits, current_time, rate_per_day, request_permits);
        }

        public string GetCacheKey(string cacheTag)
        {
            return $"ccxc:recordcache:{cacheTag}";
        }

        public string GetDataKey(string cacheKey)
        {
            return $"ccxc:datacache:{cacheKey}";
        }

        public string GetUserSessionKey(string uuid)
        {
            return $"ccxc:usersession:{uuid}";
        }

        public string GetTempTicketKey(string uuid)
        {
            return $"ccxc:tempticket:{uuid}";
        }
    }
}
