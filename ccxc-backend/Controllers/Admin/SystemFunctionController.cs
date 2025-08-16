using Ccxc.Core.HttpServer;
using Ccxc.Core.Utils;
using ccxc_backend.Controllers.Announcements;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using ccxc_backend.Functions;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Admin
{
    [Export(typeof(HttpController))]
    public class SystemFunctionController : HttpController
    {
        [HttpHandler("POST", "/admin/overview")]
        public async Task Overview(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var userDb = DbFactory.Get<User>();
            var userCount = await userDb.SimpleDb.AsQueryable().CountAsync();

            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindCount = await groupBindDb.SimpleDb.AsQueryable().CountAsync();

            var groupDb = DbFactory.Get<UserGroup>();
            var groupCount = await groupDb.SimpleDb.AsQueryable().CountAsync();

            var cache = DbFactory.GetCache();
            //登录成功
            var keyPattern = cache.GetUserSessionKey("*");
            var sessions = cache.FindKeys(keyPattern);
            var lastActionList = (await Task.WhenAll(sessions.Select(async it => await cache.Get<UserSession>(it))))
                .Where(it => it != null && it.is_active == 1)
                .GroupBy(it => it.uid)
                .Select(it => it.OrderByDescending(s => s.last_update).FirstOrDefault()?.last_update ?? DateTime.MinValue)
                .Where(it => Math.Abs((DateTime.Now - it).TotalMinutes) < 5);


            //近30天每日登录用户数统计
            var dtStart = DateTime.Now.AddDays(-30);
            var dtEnd = DateTime.Now;
            var loginDb = DbFactory.Get<LoginLog>();
            var dailyLoginCount = await loginDb.SimpleDb.AsQueryable().Where(x => x.status == 1 && x.create_time >= dtStart && x.create_time < dtEnd)
                .GroupBy(x => x.create_time.ToString("yyyy-MM-dd"))
                .Select(x => new
                {
                    d = x.create_time.ToString("yyyy-MM-dd"),
                    count = SqlFunc.AggregateDistinctCount(x.uid)
                }).ToListAsync();


            await response.JsonResponse(200, new
            {
                status = 1,
                result = new
                {
                    now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    user_count = userCount,
                    group_bind_count = groupBindCount,
                    group_count = groupCount,
                    online_count = lastActionList.Count(),
                },
                daily_login_count = dailyLoginCount
            });
        }

        [HttpHandler("POST", "/admin/purge-cache")]
        public async Task CachePurge(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Administrator);
            if (userSession == null) return;

            var requestJson = request.Json<PurgeCacheRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var annoDb = DbFactory.Get<Announcement>();
            var db = (annoDb).Db;

            switch (requestJson.op_key)
            {
                case "anno": //公告
                    {
                        db.DataCache.RemoveDataCache("Announcement");
                        db.DataCache.RemoveDataCache("TempAnno");
                        await AnnoController.ResetAnnoCache(annoDb);

                        var keyPattern = "ccxc:recordcache:read_anno_id_for_*";
                        var cache = DbFactory.GetCache();

                        var keys = cache.FindKeys(keyPattern);
                        foreach (var key in keys)
                        {
                            await cache.Delete(key);
                        }
                        await response.OK();
                        return;
                    }
                case "invi": //邀请
                    {
                        db.DataCache.RemoveDataCache("invite");
                        await response.OK();
                        return;
                    }
                case "mess": //站内信
                    {
                        db.DataCache.RemoveDataCache("message");
                        await response.OK();
                        return;
                    }
                case "prog": //进度
                    {
                        db.DataCache.RemoveDataCache("progress");
                        await response.OK();
                        return;
                    }
                case "puzz": //题目
                    {
                        db.DataCache.RemoveDataCache("puzzle");
                        db.DataCache.RemoveDataCache("additional_answer");
                        db.DataCache.RemoveDataCache("puzzle_article");
                        db.DataCache.RemoveDataCache("puzzle_backend_script");
                        await response.OK();
                        return;
                    }
                case "puzg": //题目组
                    {
                        db.DataCache.RemoveDataCache("puzzle_group");
                        await response.OK();
                        return;
                    }
                case "user": //用户
                    {
                        db.DataCache.RemoveDataCache("user");
                        await response.OK();
                        return;
                    }
                case "useg": //用户组
                    {
                        db.DataCache.RemoveDataCache("user_group");
                        await response.OK();
                        return;
                    }
                case "usgb": //用户绑定
                    {
                        db.DataCache.RemoveDataCache("user_group_bind");
                        await response.OK();
                        return;
                    }
                case "uall": //用户相关全部
                    {
                        db.DataCache.RemoveDataCache("user");
                        db.DataCache.RemoveDataCache("user_group");
                        db.DataCache.RemoveDataCache("user_group_bind");
                        await response.OK();
                        return;
                    }
                case "pall": //题目相关全部
                    {
                        db.DataCache.RemoveDataCache("puzzle");
                        db.DataCache.RemoveDataCache("puzzle_group");
                        db.DataCache.RemoveDataCache("additional_answer");
                        db.DataCache.RemoveDataCache("puzzle_article");
                        db.DataCache.RemoveDataCache("puzzle_backend_script");
                        await response.OK();
                        return;
                    }
                case "aall": //全部
                    {
                        var keyPattern = "ccxc:mysqlcache:*";
                        var cache = DbFactory.GetCache();

                        var keys = cache.FindKeys(keyPattern);
                        foreach (var key in keys)
                        {
                            await cache.Delete(key);
                        }
                        await response.OK();
                        return;
                    }
                default:
                    break;
            }

            await response.BadRequest("wrong op_key");
        }

        [HttpHandler("POST", "/admin/performance")]
        public async Task Performance(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            if (string.IsNullOrEmpty(Config.Config.Options.PrometheusApi))
            {
                await response.BadRequest("性能监控未启用，因为Prometheus API 地址未配置。");
                return;
            }

            var endTime = DateTime.Now;
            var startTime = endTime.AddHours(-2); // 2小时内的数据

            var url = Config.Config.Options.PrometheusApi + "/api/v1/query_range";

            //CPU
            var cpuResult = await HttpRequest.Get<PrometheusResponse>(url, new Dictionary<string, string>
            {
                { "query", "(1-avg(rate(node_cpu_seconds_total{mode=\"idle\"}[1m])))*100" },
                { "step", "15" },
                { "start", UnixTimestamp.GetTimestampSecond(startTime).ToString() },
                { "end", UnixTimestamp.GetTimestampSecond(endTime).ToString() }
            });

            List<TimepointData> cpuData = null;
            if (cpuResult?.data?.result != null && cpuResult.data.result.Count > 0)
            {
                cpuData = cpuResult.data.result[0].values.Select(v => new TimepointData
                {
                    ts = UnixTimestamp.FromTimestamp((long)(double.Parse(v[0].ToString()) * 1000.0)),
                    value = double.Parse(v[1].ToString())
                }).ToList();
            }

            //内存
            var memoryResult = await HttpRequest.Get<PrometheusResponse>(url, new Dictionary<string, string>
            {
                { "query", "(node_memory_MemTotal_bytes-node_memory_MemAvailable_bytes)/node_memory_MemTotal_bytes*100" },
                { "step", "15" },
                { "start", UnixTimestamp.GetTimestampSecond(startTime).ToString() },
                { "end", UnixTimestamp.GetTimestampSecond(endTime).ToString() }
            });

            List<TimepointData> memoryData = null;
            if (memoryResult?.data?.result != null && memoryResult.data.result.Count > 0)
            {
                memoryData = memoryResult.data.result[0].values.Select(v => new TimepointData
                {
                    ts = UnixTimestamp.FromTimestamp((long)(double.Parse(v[0].ToString()) * 1000.0)),
                    value = double.Parse(v[1].ToString())
                }).ToList();
            }

            //磁盘空间
            var diskSpaceResult = await HttpRequest.Get<PrometheusResponse>(url, new Dictionary<string, string>
            {
                { "query", "(node_filesystem_size_bytes{fstype=~\"ext4|xfs\"}-node_filesystem_avail_bytes{fstype=~\"ext4|xfs\"})/node_filesystem_size_bytes{fstype=~\"ext4|xfs\"}*100" },
                { "step", "15" },
                { "start", UnixTimestamp.GetTimestampSecond(startTime).ToString() },
                { "end", UnixTimestamp.GetTimestampSecond(endTime).ToString() }
            });

            List<TimepointData> diskSpaceData = null;
            if (diskSpaceResult?.data?.result != null && diskSpaceResult.data.result.Count > 0)
            {
                diskSpaceData = diskSpaceResult.data.result[0].values.Select(v => new TimepointData
                {
                    ts = UnixTimestamp.FromTimestamp((long)(double.Parse(v[0].ToString()) * 1000.0)),
                    value = double.Parse(v[1].ToString())
                }).ToList();
            }

            await response.JsonResponse(200, new PerformanceResponse
            {
                status = 1,
                cpu = cpuData ?? [],
                memory = memoryData ?? [],
                disk_space = diskSpaceData ?? [],
            });
        }
    }
}
