using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Ccxc.Core.Utils
{
    /// <summary>
    /// Redis连接池管理器
    /// 用于管理多个ConnectionMultiplexer实例以提升高并发场景下的性能
    /// </summary>
    public class RedisConnectionPool : IDisposable
    {
        private readonly string _connectionString;
        private readonly int _poolSize;
        private readonly ConcurrentQueue<ConnectionMultiplexer> _pool;
        private readonly SemaphoreSlim _semaphore;
        private readonly object _lockObject = new object();
        private volatile bool _disposed = false;
        private int _currentConnections = 0;
        private readonly Timer _healthCheckTimer;

        /// <summary>
        /// 初始化Redis连接池
        /// </summary>
        /// <param name="connectionString">Redis连接字符串</param>
        /// <param name="poolSize">连接池大小</param>
        public RedisConnectionPool(string connectionString, int poolSize = 5)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("连接字符串不能为空", nameof(connectionString));
            if (poolSize <= 0)
                throw new ArgumentException("连接池大小必须大于0", nameof(poolSize));

            _connectionString = connectionString;
            _poolSize = poolSize;
            _pool = new ConcurrentQueue<ConnectionMultiplexer>();
            _semaphore = new SemaphoreSlim(poolSize, poolSize);

            // 启动健康检查定时器，每5分钟检查一次连接池健康状态
            _healthCheckTimer = new Timer(PerformHealthCheck, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            Logger.Info($"Redis连接池已初始化，连接池大小：{poolSize}");
        }

        /// <summary>
        /// 从连接池获取一个ConnectionMultiplexer实例
        /// </summary>
        /// <param name="timeoutMs">获取连接的超时时间（毫秒），默认5000ms</param>
        /// <returns>ConnectionMultiplexer实例</returns>
        public async Task<ConnectionMultiplexer> GetConnectionAsync(int timeoutMs = 5000)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RedisConnectionPool));

            // 使用超时控制避免无限等待
            using (var cts = new CancellationTokenSource(timeoutMs))
            {
                try
                {
                    await _semaphore.WaitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException($"获取Redis连接超时（{timeoutMs}ms），连接池可能已满");
                }
            }

            try
            {
                // 尝试从池中获取连接
                if (_pool.TryDequeue(out ConnectionMultiplexer connection))
                {
                    // 检查连接是否仍然有效
                    if (connection != null && connection.IsConnected)
                    {
                        // 从池中获取到有效连接，直接返回（信号量已经被占用，等ReturnConnection时释放）
                        return connection;
                    }
                    else
                    {
                        // 连接无效，需要创建新连接
                        connection?.Dispose();
                        Interlocked.Decrement(ref _currentConnections);
                    }
                }

                // 池中没有可用连接，创建新连接
                var newConnection = await CreateNewConnectionAsync();
                // 新连接创建成功，信号量保持占用状态，等ReturnConnection时释放
                return newConnection;
            }
            catch
            {
                // 发生异常时必须释放信号量
                _semaphore.Release();
                throw;
            }
        }

        /// <summary>
        /// 将ConnectionMultiplexer实例归还到连接池
        /// </summary>
        /// <param name="connection">要归还的ConnectionMultiplexer实例</param>
        public void ReturnConnection(ConnectionMultiplexer connection)
        {
            if (_disposed || connection == null)
            {
                connection?.Dispose();
                _semaphore.Release();
                return;
            }

            // 检查连接是否仍然有效
            if (connection.IsConnected)
            {
                _pool.Enqueue(connection);
            }
            else
            {
                connection.Dispose();
                Interlocked.Decrement(ref _currentConnections);
            }

            _semaphore.Release();
        }

        /// <summary>
        /// 创建新的Redis连接
        /// </summary>
        /// <returns>新的ConnectionMultiplexer实例</returns>
        private async Task<ConnectionMultiplexer> CreateNewConnectionAsync()
        {
            try
            {
                var configOptions = ConfigurationOptions.Parse(_connectionString);
                configOptions.AbortOnConnectFail = false;
                configOptions.ConnectTimeout = 5000; // 5秒连接超时
                configOptions.SyncTimeout = 3000;    // 3秒同步操作超时
                configOptions.AsyncTimeout = 3000;   // 3秒异步操作超时

                var connection = await ConnectionMultiplexer.ConnectAsync(configOptions);
                
                // 注册连接失败事件
                connection.ConnectionFailed += (sender, args) =>
                {
                    Logger.Error($"Redis连接失败: {args.Exception?.Message ?? "未知错误"}");
                };

                // 注册连接恢复事件
                connection.ConnectionRestored += (sender, args) =>
                {
                    Logger.Info("Redis连接已恢复");
                };

                Interlocked.Increment(ref _currentConnections);
                Logger.Debug($"创建新的Redis连接，当前连接数：{_currentConnections}");
                
                return connection;
            }
            catch (Exception ex)
            {
                Logger.Error($"创建Redis连接失败: {ex.Message}");
                throw new InvalidOperationException($"无法创建Redis连接: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取连接池状态信息
        /// </summary>
        /// <returns>连接池状态</returns>
        public PoolStatus GetPoolStatus()
        {
            return new PoolStatus
            {
                PoolSize = _poolSize,
                CurrentConnections = _currentConnections,
                AvailableConnections = _pool.Count,
                AvailableSlots = _semaphore.CurrentCount
            };
        }

        /// <summary>
        /// 执行连接池健康检查
        /// </summary>
        /// <param name="state">定时器状态</param>
        private void PerformHealthCheck(object state)
        {
            if (_disposed)
                return;

            try
            {
                var invalidConnections = new List<ConnectionMultiplexer>();
                var tempConnections = new List<ConnectionMultiplexer>();

                // 检查池中的所有连接
                while (_pool.TryDequeue(out ConnectionMultiplexer connection))
                {
                    if (connection != null && connection.IsConnected)
                    {
                        tempConnections.Add(connection);
                    }
                    else
                    {
                        invalidConnections.Add(connection);
                    }
                }

                // 将有效连接放回池中
                foreach (var connection in tempConnections)
                {
                    _pool.Enqueue(connection);
                }

                // 清理无效连接
                foreach (var connection in invalidConnections)
                {
                    try
                    {
                        connection?.Dispose();
                        Interlocked.Decrement(ref _currentConnections);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"清理无效Redis连接时发生错误: {ex.Message}");
                    }
                }

                if (invalidConnections.Count > 0)
                {
                    Logger.Info($"健康检查清理了 {invalidConnections.Count} 个无效连接");
                }

                // 记录连接池状态
                var status = GetPoolStatus();
                Logger.Debug($"连接池健康检查完成 - 池大小:{status.PoolSize}, 当前连接:{status.CurrentConnections}, 可用连接:{status.AvailableConnections}, 可用槽位:{status.AvailableSlots}");
            }
            catch (Exception ex)
            {
                Logger.Error($"连接池健康检查失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lockObject)
            {
                if (_disposed)
                    return;

                _disposed = true;
            }

            Logger.Info("正在释放Redis连接池资源...");

            // 停止健康检查定时器
            try
            {
                _healthCheckTimer?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error($"释放健康检查定时器时发生错误: {ex.Message}");
            }

            // 释放所有连接
            while (_pool.TryDequeue(out ConnectionMultiplexer connection))
            {
                try
                {
                    connection?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Error($"释放Redis连接时发生错误: {ex.Message}");
                }
            }

            _semaphore?.Dispose();
            Logger.Info("Redis连接池资源释放完成");
        }
    }

    /// <summary>
    /// 连接池状态信息
    /// </summary>
    public class PoolStatus
    {
        /// <summary>
        /// 连接池大小
        /// </summary>
        public int PoolSize { get; set; }

        /// <summary>
        /// 当前连接数
        /// </summary>
        public int CurrentConnections { get; set; }

        /// <summary>
        /// 可用连接数
        /// </summary>
        public int AvailableConnections { get; set; }

        /// <summary>
        /// 可用连接槽位数
        /// </summary>
        public int AvailableSlots { get; set; }
    }
}