using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Ccxc.Core.Utils
{
    /// <summary>
    /// Redis缓存客户端
    /// 使用连接池管理ConnectionMultiplexer实例以提升高并发性能
    /// </summary>
    public class RedisClient : IDisposable
    {
        private static RedisConnectionPool _connectionPool;
        private static readonly object _lockObject = new object();
        private ConnectionMultiplexer _currentConnection;
        private readonly int _database;
        private bool _disposed = false;

        /// <summary>
        /// 终结器，确保连接被释放（防止连接泄漏）
        /// </summary>
        ~RedisClient()
        {
            Dispose();
        }

        /// <summary>
        /// Redis数据库实例
        /// </summary>
        public IDatabase RedisDb { get; private set; }

        /// <summary>
        /// 初始化连接池（静态方法，整个应用程序只需调用一次）
        /// </summary>
        /// <param name="connStr">Redis数据库连接字符串</param>
        /// <param name="poolSize">连接池大小</param>
        public static void InitializeConnectionPool(string connStr, int poolSize = 5)
        {
            if (_connectionPool == null)
            {
                lock (_lockObject)
                {
                    if (_connectionPool == null)
                    {
                        _connectionPool = new RedisConnectionPool(connStr, poolSize);
                        Logger.Info($"Redis连接池已初始化，连接字符串：{connStr}，连接池大小：{poolSize}");
                    }
                }
            }
        }

        /// <summary>
        /// 获得一个Redis缓存数据库对象
        /// </summary>
        /// <param name="connStr">Redis数据库连接字符串</param>
        /// <param name="database">指定数据库，默认为0</param>
        /// <param name="poolSize">连接池大小（仅在首次初始化时有效）</param>
        /// <param name="autoInitialize">是否自动初始化连接（默认false，保持向后兼容）</param>
        public RedisClient(string connStr, int database = 0, int poolSize = 5, bool autoInitialize = false)
        {
            _database = database;
            
            // 确保连接池已初始化
            InitializeConnectionPool(connStr, poolSize);

            // 如果启用自动初始化，立即获取连接
            if (autoInitialize)
            {
                try
                {
                    InitializeAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Redis客户端自动初始化失败: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// 创建一个已初始化的Redis客户端（推荐使用此方法）
        /// </summary>
        /// <param name="connStr">Redis数据库连接字符串</param>
        /// <param name="database">指定数据库，默认为0</param>
        /// <param name="poolSize">连接池大小（仅在首次初始化时有效）</param>
        /// <returns>已初始化的Redis客户端</returns>
        public static async Task<RedisClient> CreateAsync(string connStr, int database = 0, int poolSize = 5)
        {
            var client = new RedisClient(connStr, database, poolSize);
            await client.InitializeAsync();
            return client;
        }

        /// <summary>
        /// 创建一个已初始化的Redis客户端（同步版本，适用于using模式）
        /// </summary>
        /// <param name="connStr">Redis数据库连接字符串</param>
        /// <param name="database">指定数据库，默认为0</param>
        /// <param name="poolSize">连接池大小（仅在首次初始化时有效）</param>
        /// <returns>已初始化的Redis客户端</returns>
        public static RedisClient CreateInitialized(string connStr, int database = 0, int poolSize = 5)
        {
            return new RedisClient(connStr, database, poolSize, autoInitialize: true);
        }

        /// <summary>
        /// 异步初始化Redis连接
        /// </summary>
        /// <returns></returns>
        public async Task InitializeAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RedisClient));

            if (_currentConnection == null)
            {
                _currentConnection = await _connectionPool.GetConnectionAsync();
                RedisDb = _currentConnection.GetDatabase(_database);
            }
        }

        /// <summary>
        /// 确保连接已初始化
        /// </summary>
        /// <returns></returns>
        private async Task EnsureInitializedAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RedisClient));
                
            if (_currentConnection == null || RedisDb == null)
            {
                await InitializeAsync();
            }
        }

        /// <summary>
        /// 同步方式确保连接已初始化（仅用于内部，不推荐外部使用）
        /// </summary>
        private void EnsureInitializedSync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RedisClient));
                
            if (_currentConnection == null || RedisDb == null)
            {
                // 使用GetAwaiter().GetResult()进行同步等待
                InitializeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// 存入一个字符串类型的数据
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <param name="timeoutMilliseconds">超时时间（毫秒），若设为小于等于0的值，则无超时。默认为-1</param>
        public async Task PutString(string key, string value, long timeoutMilliseconds = -1)
        {
            await EnsureInitializedAsync();
            
            if (timeoutMilliseconds > 0)
            {
                await RedisDb.StringSetAsync(key, value, TimeSpan.FromMilliseconds(timeoutMilliseconds));
            }
            else
            {
                await RedisDb.StringSetAsync(key, value);
            }
        }

        /// <summary>
        /// 存入一个对象，使用json序列化
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <param name="timeoutMilliseconds">超时时间（毫秒），若设为小于等于0的值，则无超时。默认为-1</param>
        public async Task PutObject(string key, object value, long timeoutMilliseconds = -1)
        {
            await EnsureInitializedAsync();
            
            var objectString = JsonConvert.SerializeObject(value);
            if (timeoutMilliseconds > 0)
            {
                await RedisDb.StringSetAsync(key, objectString, TimeSpan.FromMilliseconds(timeoutMilliseconds));
            }
            else
            {
                await RedisDb.StringSetAsync(key, objectString);
            }
        }

        /// <summary>
        /// 取得一个字符串
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>Value</returns>
        public async Task<string> GetString(string key)
        {
            await EnsureInitializedAsync();
            
            string value = await RedisDb.StringGetAsync(key);
            return value;
        }

        /// <summary>
        /// 取得一个对象
        /// 使用json反序列化
        /// </summary>
        /// <typeparam name="T">对象的类型</typeparam>
        /// <param name="key">Key</param>
        /// <returns>取得的对象</returns>
        public async Task<T> GetObject<T>(string key)
        {
            await EnsureInitializedAsync();
            
            string valueString = await RedisDb.StringGetAsync(key);
            if (valueString == null) return default;
            return JsonConvert.DeserializeObject<T>(valueString);
        }

        /// <summary>
        /// 存入一个哈希表
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="hashset">哈希表集合（哈希表条目object使用json序列化）</param>
        /// <param name="timeoutMilliseconds">超时时间（毫秒），若设为小于等于0的值，则无超时。默认为-1</param>
        /// <returns></returns>
        public async Task PutHash(string key, IDictionary<string, object> hashset, long timeoutMilliseconds = -1)
        {
            var hashValues = hashset.Select(kv =>
            {
                string hkey = kv.Key;
                string value = JsonConvert.SerializeObject(kv.Value);
                return new HashEntry(hkey, value);
            });
            await EnsureInitializedAsync();
            
            await RedisDb.HashSetAsync(key, hashValues.ToArray());
            if (timeoutMilliseconds > 0)
            {
                await RedisDb.KeyExpireAsync(key, TimeSpan.FromMilliseconds(timeoutMilliseconds));
            }
        }
        public async Task PutHashObject(string key, string hashKey, object value)
        {
            await EnsureInitializedAsync();
            
            var objectString = JsonConvert.SerializeObject(value);
            await RedisDb.HashSetAsync(key, hashField: hashKey, value: objectString);
        }

        /// <summary>
        /// 取出整个哈希表集合
        /// </summary>
        /// <typeparam name="T">哈希表条目的数据类型（使用json反序列化）</typeparam>
        /// <param name="key">Key</param>
        /// <returns>List&lt;<typeparamref name="T"/>>类型的整个哈希表集合。</returns>
        public async Task<List<T>> GetHashAll<T>(string key)
        {
            await EnsureInitializedAsync();
            
            var hashset = await RedisDb.HashGetAllAsync(key);
            return hashset.Select(hashitem =>
            {
                return JsonConvert.DeserializeObject<T>(hashitem.Value);
            }).ToList();
        }

        public async Task<List<(string key, T value)>> GetHashKeys<T>(string key)
        {
            await EnsureInitializedAsync();
            
            var hashset = await RedisDb.HashGetAllAsync(key);
            return hashset.Select(hashitem =>
            {
                string hashKey = hashitem.Name;
                T hashvalue = JsonConvert.DeserializeObject<T>(hashitem.Value);
                return (hashKey, hashvalue);
            }).ToList();
        }

        /// <summary>
        /// 取得哈希表集合的一个条目
        /// </summary>
        /// <typeparam name="T">条目的数据类型（使用json反序列化）</typeparam>
        /// <param name="key">Key</param>
        /// <param name="field">主键</param>
        /// <returns>取出的哈希表条目对象</returns>
        public async Task<T> GetHash<T>(string key, string field)
        {
            await EnsureInitializedAsync();
            
            string hashvalue = await RedisDb.HashGetAsync(key, field);
            if (hashvalue == null) return default;
            return JsonConvert.DeserializeObject<T>(hashvalue);
        }

        public async Task PutList(string key, IList<object> list, long timeoutMilliseconds = -1)
        {
            await EnsureInitializedAsync();
            
            var listValues = list.Select(it => (RedisValue)(JsonConvert.SerializeObject(it))).ToArray();
            await RedisDb.ListRightPushAsync(key, listValues);
            if (timeoutMilliseconds > 0)
            {
                await RedisDb.KeyExpireAsync(key, TimeSpan.FromMilliseconds(timeoutMilliseconds));
            }
        }

        public async Task<List<T>> GetList<T>(string key, int start, int end, long updateTimeout = -1)
        {
            await EnsureInitializedAsync();
            
            var rangeResult = await RedisDb.ListRangeAsync(key, start, end);
            if (updateTimeout > 0)
            {
                await RedisDb.KeyExpireAsync(key, TimeSpan.FromMilliseconds(updateTimeout));
            }
            return rangeResult.Select(it => JsonConvert.DeserializeObject<T>(it.ToString())).ToList();
        }

        public async Task<long> GetListLength(string key)
        {
            await EnsureInitializedAsync();
            
            return await RedisDb.ListLengthAsync(key);
        }

        public async Task<long> GetHashLength(string key)
        {
            await EnsureInitializedAsync();
            
            return await RedisDb.HashLengthAsync(key);
        }

        /// <summary>
        /// 将一个特定的Key标记为无效
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task Delete(string key)
        {
            await EnsureInitializedAsync();
            
            await RedisDb.KeyDeleteAsync(key);
        }

        public async Task Delete(string key, string hkey)
        {
            await EnsureInitializedAsync();
            
            await RedisDb.HashDeleteAsync(key, hkey);
        }

        public async Task<bool> Exists(string key)
        {
            await EnsureInitializedAsync();
            
            return await RedisDb.KeyExistsAsync(key);
        }
        public async Task<bool> Exists(string key, string hashKey)
        {
            await EnsureInitializedAsync();
            
            return await RedisDb.HashExistsAsync(key, hashKey);
        }

        /// <summary>
        /// 根据KeyPattern找出所有匹配的key
        /// 注意：本方法有性能问题，在集群中使用可能无法返回所有的key。本方法无法以异步方式运行，请使用同步模式调用。
        /// </summary>
        /// <param name="keyPattern"></param>
        /// <returns></returns>
        public async Task<List<string>> FindKeys(string keyPattern)
        {
            await EnsureInitializedAsync();
            
            var endpoints = _currentConnection?.GetEndPoints();
            if (endpoints != null && endpoints.Length > 0)
            {
                var server = _currentConnection?.GetServer(endpoints[0]);
                return server.Keys(_database, keyPattern, 10, CommandFlags.None).Select(k => (string)k).ToList();
            }
            else
            {
                return new List<string>();
            }
        }

        public async Task<List<(string key, T value)>> SearchHashKey<T>(string key, string pattern)
        {
            await EnsureInitializedAsync();
            
            var result = await Task.Run(() =>
            {
                var cpattern = $"*{pattern}*";
                return RedisDb.HashScan(key, cpattern);
            });
            return result.Select(hashitem =>
            {
                string hashKey = hashitem.Name;
                T hashvalue = JsonConvert.DeserializeObject<T>(hashitem.Value);
                return (hashKey, hashvalue);
            }).ToList();
        }

        public async Task<List<(string key, T value)>> SearchHashValue<T>(string key, string pattern)
        {
            await EnsureInitializedAsync();
            
            var result = await RedisDb.ScriptEvaluateAsync(LuaScript.Prepare(
                "local ks = redis.call('hgetall', @key)\n" +
                "local result = {}\n" +
                "for i=1,#ks,2 do\n" +
                "    if string.find(ks[i + 1], @pattern) then\n" +
                "        result[#result + 1] = ks[i]\n" +
                "        result[#result + 1] = ks[i + 1]\n" +
                "    end\n" +
                "end\n" +
                "return result\n"
                ), new { key, pattern });
            var k = (string[])result;

            var res = new List<(string, T)>();
            for (var i = 0; i < k.Length; i += 2)
            {
                var value = JsonConvert.DeserializeObject<T>(k[i + 1]);
                res.Add((k[i], value));
            }

            return res;
        }

        /// <summary>
        /// 流量控制
        /// </summary>
        /// <param name="key">Redis存储键名</param>
        /// <param name="max_permits">最大授权令牌数量</param>
        /// <param name="current_time">当前时间戳（毫秒）</param>
        /// <param name="rate_per_day">每日恢复的令牌数量（每日最大流控值）</param>
        /// <param name="request_permits">本次请求的令牌数量</param>
        /// <returns></returns>
        public async Task<int> RateLimiter(string key, int max_permits, long current_time, int rate_per_day, int request_permits)
        {
            try
            {


                await EnsureInitializedAsync();
                
                var result = await RedisDb.ScriptEvaluateAsync(LuaScript.Prepare(@"
local para_key = @key
local para_max_permits = @max_permits
local para_current_time = @current_time
local para_rate_per_day = @rate_per_day
local para_request_permits = @request_permits
local rate_limit_info = redis.pcall(""HMGET"", para_key, ""last_update_time"", ""curr_permits"")
local last_update_time = tonumber(rate_limit_info[1])
local curr_permits = tonumber(rate_limit_info[2])

--- 初始化令牌桶为全满
local available_permits = para_max_permits

--- 读取上次更新时间，添加令牌桶
if (type(last_update_time) ~= ""boolean"" and last_update_time ~= false and last_update_time ~= nil) then
    local recovered_permits = math.floor(((para_current_time - last_update_time) / 86400000) * para_rate_per_day)
    local expect_curr_permits = recovered_permits + curr_permits
    available_permits = math.min(expect_curr_permits, para_max_permits)
end

--- 扣减令牌数量
local result = 0
if (available_permits - para_request_permits >= 0) then
    result = para_request_permits
    available_permits = available_permits - para_request_permits
end

--- 回写并写入到期时间
redis.pcall(""HMSET"", para_key, ""curr_permits"", available_permits, ""last_update_time"", para_current_time)
redis.pcall(""PEXPIRE"", para_key, math.ceil(((para_max_permits - available_permits) / (para_rate_per_day / 86400000)) + 10000))

return result
                "), new { key, max_permits, current_time, rate_per_day, request_permits });
                var sResult = (string)result;
                int.TryParse(sResult, out int res);
                return res;
            }
            catch (RedisConnectionException e)
            {
                Logger.Error("Redis连接断开。" + e.ToString());
                throw new Exception("Redis连接失败" + e.Message, e);
            }
        }

        public async Task<ISubscriber> GetSubscriber()
        {
            await EnsureInitializedAsync();
            
            return _currentConnection?.GetSubscriber();
        }

        /// <summary>
        /// 获取连接池状态信息
        /// </summary>
        /// <returns>连接池状态</returns>
        public static PoolStatus GetPoolStatus()
        {
            return _connectionPool?.GetPoolStatus() ?? new PoolStatus();
        }

        /// <summary>
        /// 释放当前Redis连接实例
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 受保护的释放方法
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // 释放托管资源
                if (_currentConnection != null)
                {
                    try
                    {
                        _connectionPool?.ReturnConnection(_currentConnection);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"归还Redis连接到连接池时发生错误: {ex.Message}");
                    }
                    _currentConnection = null;
                }

                RedisDb = null;
            }

            _disposed = true;
        }

        /// <summary>
        /// 释放整个连接池（应用程序关闭时调用）
        /// </summary>
        public static void DisposeConnectionPool()
        {
            _connectionPool?.Dispose();
            _connectionPool = null;
        }
    }
}
