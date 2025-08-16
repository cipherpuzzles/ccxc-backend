using Ccxc.Core.Utils;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ccxc.Core.DbOrm
{
    public class RedisCache : ICacheService
    {
        protected RedisCacheConfig Config;
        protected int DefaultCacheDurationInSeconds;
        public RedisCache(RedisCacheConfig config, int defaultCacheDurationInSeconds = 86400)
        {
            Config = config;
            DefaultCacheDurationInSeconds = defaultCacheDurationInSeconds;
        }

        public void Add<V>(string key, V value)
        {
            using var redis = RedisClient.CreateInitialized(Config.RedisConnStr, Config.RedisDatabase);
            var rkey = $"ccxc:mysqlcache:{key}";
            redis.PutObject(rkey, value, DefaultCacheDurationInSeconds * 1000).GetAwaiter().GetResult();
        }

        public void Add<V>(string key, V value, int cacheDurationInSeconds)
        {
            using var redis = RedisClient.CreateInitialized(Config.RedisConnStr, Config.RedisDatabase);
            var rkey = $"ccxc:mysqlcache:{key}";
            redis.PutObject(rkey, value, cacheDurationInSeconds * 1000).GetAwaiter().GetResult();
        }

        public bool ContainsKey<V>(string key)
        {
            using var redis = RedisClient.CreateInitialized(Config.RedisConnStr, Config.RedisDatabase);
            var rkey = $"ccxc:mysqlcache:{key}";
            return redis.Exists(rkey).GetAwaiter().GetResult();
        }

        public V Get<V>(string key)
        {
            using var redis = RedisClient.CreateInitialized(Config.RedisConnStr, Config.RedisDatabase);
            var rkey = $"ccxc:mysqlcache:{key}";
            return redis.GetObject<V>(rkey).GetAwaiter().GetResult();
        }

        public IEnumerable<string> GetAllKey<V>()
        {
            using var redis = RedisClient.CreateInitialized(Config.RedisConnStr, Config.RedisDatabase);
            var keyPattern = $"ccxc:mysqlcache:*";
            var keyPrefix = $"ccxc:mysqlcache:";
            var keys = redis.FindKeys(keyPattern).GetAwaiter().GetResult();
            return keys.Select(k => k.Replace(keyPrefix, ""));
        }

        public V GetOrCreate<V>(string cacheKey, Func<V> create, int cacheDurationInSeconds = int.MaxValue)
        {
            if (ContainsKey<V>(cacheKey))
            {
                return Get<V>(cacheKey);
            }
            else
            {
                var result = create.Invoke();
                var cacheTime = DefaultCacheDurationInSeconds;
                if (cacheDurationInSeconds != int.MaxValue && cacheDurationInSeconds > 0)
                {
                    cacheTime = cacheDurationInSeconds;
                }
                Add(cacheKey, result, cacheTime);
                return result;
            }
        }

        public void Remove<V>(string key)
        {
            using var redis = RedisClient.CreateInitialized(Config.RedisConnStr, Config.RedisDatabase);
            var rkey = $"ccxc:mysqlcache:{key}";
            redis.Delete(rkey).GetAwaiter().GetResult();
        }
    }

    public class RedisCacheConfig
    {
        public string RedisConnStr { get; set; }
        public int RedisDatabase { get; set; }
    }
}
