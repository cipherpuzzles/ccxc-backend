using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Ccxc.Core.Utils;

namespace Ccxc.Core.HttpServer
{
    /// <summary>
    /// HTTP服务器性能监控器
    /// 用于监控连接数、请求处理时间等关键指标
    /// </summary>
    public static class PerformanceMonitor
    {
        private static long _activeConnections = 0;
        private static long _totalRequests = 0;
        private static long _totalErrors = 0;
        private static readonly ConcurrentDictionary<string, RequestMetrics> _endpointMetrics = new();
        private static Timer _reportTimer;
        
        static PerformanceMonitor()
        {
            // 每30秒输出一次性能报告
            _reportTimer = new Timer(ReportMetrics, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// 记录新连接
        /// </summary>
        public static void RecordConnection()
        {
            Interlocked.Increment(ref _activeConnections);
        }

        /// <summary>
        /// 记录连接关闭
        /// </summary>
        public static void RecordDisconnection()
        {
            Interlocked.Decrement(ref _activeConnections);
        }

        /// <summary>
        /// 记录请求开始
        /// </summary>
        /// <param name="endpoint">端点路径</param>
        /// <returns>请求跟踪ID</returns>
        public static string RecordRequestStart(string endpoint)
        {
            Interlocked.Increment(ref _totalRequests);
            var requestId = Guid.NewGuid().ToString("N")[..8];
            
            _endpointMetrics.AddOrUpdate(endpoint, 
                new RequestMetrics { Count = 1, TotalTime = 0 },
                (key, existing) => 
                {
                    existing.Count++;
                    return existing;
                });
            
            return requestId;
        }

        /// <summary>
        /// 记录请求完成
        /// </summary>
        /// <param name="endpoint">端点路径</param>
        /// <param name="requestId">请求跟踪ID</param>
        /// <param name="processingTimeMs">处理时间（毫秒）</param>
        /// <param name="isError">是否为错误请求</param>
        public static void RecordRequestEnd(string endpoint, string requestId, long processingTimeMs, bool isError = false)
        {
            if (isError)
            {
                Interlocked.Increment(ref _totalErrors);
            }

            _endpointMetrics.AddOrUpdate(endpoint,
                new RequestMetrics { Count = 0, TotalTime = processingTimeMs },
                (key, existing) =>
                {
                    existing.TotalTime += processingTimeMs;
                    return existing;
                });
        }

        /// <summary>
        /// 获取当前性能指标
        /// </summary>
        /// <returns>性能指标</returns>
        public static PerformanceStats GetStats()
        {
            return new PerformanceStats
            {
                ActiveConnections = _activeConnections,
                TotalRequests = _totalRequests,
                TotalErrors = _totalErrors,
                ErrorRate = _totalRequests > 0 ? (double)_totalErrors / _totalRequests : 0,
                EndpointStats = new ConcurrentDictionary<string, RequestMetrics>(_endpointMetrics)
            };
        }

        /// <summary>
        /// 定期输出性能报告
        /// </summary>
        private static void ReportMetrics(object state)
        {
            try
            {
                var stats = GetStats();
                Logger.Info($"性能监控报告 - 活跃连接: {stats.ActiveConnections}, " +
                           $"总请求数: {stats.TotalRequests}, " +
                           $"错误数: {stats.TotalErrors}, " +
                           $"错误率: {stats.ErrorRate:P2}");

                // 输出Redis连接池状态
                var redisStats = RedisClient.GetPoolStatus();
                Logger.Info($"Redis连接池状态 - 池大小: {redisStats.PoolSize}, " +
                           $"当前连接: {redisStats.CurrentConnections}, " +
                           $"可用连接: {redisStats.AvailableConnections}, " +
                           $"可用槽位: {redisStats.AvailableSlots}");
            }
            catch (Exception ex)
            {
                Logger.Error($"性能监控报告生成失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 请求指标
    /// </summary>
    public class RequestMetrics
    {
        public long Count { get; set; }
        public long TotalTime { get; set; }
        public double AverageTime => Count > 0 ? (double)TotalTime / Count : 0;
    }

    /// <summary>
    /// 性能统计信息
    /// </summary>
    public class PerformanceStats
    {
        public long ActiveConnections { get; set; }
        public long TotalRequests { get; set; }
        public long TotalErrors { get; set; }
        public double ErrorRate { get; set; }
        public ConcurrentDictionary<string, RequestMetrics> EndpointStats { get; set; }
    }
}