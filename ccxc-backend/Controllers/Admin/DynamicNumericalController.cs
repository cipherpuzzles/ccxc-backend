using Ccxc.Core.HttpServer;
using ccxc_backend.Controllers.Game;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using ccxc_backend.Functions;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Admin
{
    [Export(typeof(HttpController))]
    public class DynamicNumericalController : HttpController
    {
        [HttpHandler("POST", "/admin/get-dynamic-numerical-set")]
        public async Task GetDynamicNumericalSet(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var result = new DynamicNumerical
            {
                initial_power_point = RedisNumberCenter.InitialPowerPoint, // 初始能量点
                power_increase_rate = RedisNumberCenter.PowerIncreaseRate, // 能量点增长速率
                add_attempts_count_cost = RedisNumberCenter.AddAttemptsCountCost, // 增加一倍尝试次数消耗的能量点
                unlock_tip_function_after = RedisNumberCenter.UnlockTipFunctionAfter, // 提示功能解锁时间（分钟）
                manual_tip_reply_delay = RedisNumberCenter.ManualTipReplyDelay, // 人工提示反馈的延迟时间（分钟）
                default_oracle_cost = RedisNumberCenter.DefaultOracleCost, // 默认人工提示消耗能量点
                initial_group_count = RedisNumberCenter.InitialGroupCount, // 初始解锁分区数量
                first_unlock_each_group_count = RedisNumberCenter._firstUnlockEachGroupCount,
                unlock_meta_each_group_count = RedisNumberCenter._unlockMetaEachGroupCount,
                unlock_next_group_count = RedisNumberCenter._unlockNextGroupCount,
                max_auto_unlock_group = RedisNumberCenter.MaxAutoUnlockGroup, // 最大自动解锁分区数量
            };

            await response.JsonResponse(200, new GetDynamicNumericalResponse
            {
                status = 1,
                data = result
            });
        }

        [HttpHandler("POST", "/admin/update-dynamic-numerical-set")]
        public async Task UpdateDynamicNumericalSet(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<DynamicNumerical>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //读取系统分区数量（不含隐藏分区）
            var puzzleGroupDb = DbFactory.Get<PuzzleGroup>();
            var pgList = await puzzleGroupDb.SimpleDb.AsQueryable().Where(pg => pg.is_hide == 0).WithCache().ToListAsync();
            var pgCount = pgList.Count;

            //检查first_unlock_each_group_count和unlock_meta_each_group_count是否合法
            try
            {
                if (!string.IsNullOrEmpty(requestJson.first_unlock_each_group_count))
                {
                    var fueg = requestJson.first_unlock_each_group_count.Split(',').Select(x => int.Parse(x)).ToList();
                    if (fueg.Count != pgCount)
                    {
                        await response.BadRequest("请输入与题目分区数相同数量的配置结果，用英文逗号分隔，如： 1,2,3,4,5,6,7 ");
                        return;
                    }
                }
                if (!string.IsNullOrEmpty(requestJson.unlock_meta_each_group_count))
                {
                    var umeg = requestJson.unlock_meta_each_group_count.Split(',').Select(x => int.Parse(x)).ToList();
                    if (umeg.Count != pgCount)
                    {
                        await response.BadRequest("请输入与题目分区数相同数量的配置结果，用英文逗号分隔，如： 1,2,3,4,5,6,7 ");
                        return;
                    }
                }
                if (!string.IsNullOrEmpty(requestJson.unlock_next_group_count))
                {
                    var uneg = requestJson.unlock_next_group_count.Split(',').Select(x => int.Parse(x)).ToList();
                    if (uneg.Count != pgCount)
                    {
                        await response.BadRequest("请输入与题目分区数相同数量的配置结果，用英文逗号分隔，如： 1,2,3,4,5,6,7 ");
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                await response.BadRequest("请输入正确的配置项，每项应为纯数字，与题目分区数量相同，用英文逗号分隔，如： 1,2,3,4,5,6,7 ");
                return;
            }

            RedisNumberCenter.InitialPowerPoint = requestJson.initial_power_point;
            RedisNumberCenter.AddAttemptsCountCost = requestJson.add_attempts_count_cost;
            RedisNumberCenter.UnlockTipFunctionAfter = requestJson.unlock_tip_function_after;
            RedisNumberCenter.ManualTipReplyDelay = requestJson.manual_tip_reply_delay;
            RedisNumberCenter.DefaultOracleCost = requestJson.default_oracle_cost;
            RedisNumberCenter.InitialGroupCount = requestJson.initial_group_count;
            RedisNumberCenter._firstUnlockEachGroupCount = requestJson.first_unlock_each_group_count;
            RedisNumberCenter._unlockMetaEachGroupCount = requestJson.unlock_meta_each_group_count;
            RedisNumberCenter._unlockNextGroupCount = requestJson.unlock_next_group_count;

            await response.OK();
        }

        [HttpHandler("POST", "/admin/update-power-increase-rate")]
        public async Task UpdatePowerIncreaseRate(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Administrator);
            if (userSession == null) return;

            var requestJson = request.Json<DynamicNumerical>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //刷新全员当前能量点
            //遍历所有progress
            var progressDb = DbFactory.Get<Progress>();
            var progressList = await progressDb.SimpleDb.AsQueryable().ToListAsync();

            foreach (var progressItem in progressList)
            {
                await Functions.PowerPoint.PowerPoint.UpdatePowerPoint(progressDb, progressItem.gid, 0);
            }

            //更新能量增速
            RedisNumberCenter.PowerIncreaseRate = requestJson.power_increase_rate;

            await response.OK();
        }

        [HttpHandler("POST", "/admin/unlock-auto-group")]
        public async Task UnlockAutoGroup(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Administrator);
            if (userSession == null) return;

            var requestJson = request.Json<DynamicNumerical>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var newPgid = requestJson.max_auto_unlock_group;
            if (newPgid == 0)
            {
                RedisNumberCenter.MaxAutoUnlockGroup = 0;
                await response.OK();
                return;
            }
            else if (newPgid < 0 || (newPgid > 0 && newPgid <= 2))
            {
                await response.BadRequest("必须从3开始");
                return;
            }

            //刷新全员当前能量点
            //遍历所有progress
            var progressDb = DbFactory.Get<Progress>();
            var progressList = await progressDb.SimpleDb.AsQueryable().ToListAsync();

            var puzzleDb = DbFactory.Get<Puzzle>();
            var puzzleList = await puzzleDb.SimpleDb.AsQueryable().WithCache().ToListAsync();
            var now = DateTime.Now;

            foreach (var progressItem in progressList)
            {
                //为当前队伍各分区解锁一题
                /*
                for (var i = 1; i <= 7; i++)
                {
                    if (progressItem.data.UnlockedGroups.Contains(i))
                    {
                        progressItem.data.UnlockNextPuzzle(i, puzzleList, now);
                    }
                }
                */

                //如果当前队伍已完成新手区，则为当前队伍解锁新分区
                if (progressItem.data.FinishedGroups.Contains(1) && !progressItem.data.UnlockedGroups.Contains(newPgid))
                {
                    progressItem.data.UnlockGroup(newPgid, puzzleList, now);

                    //发送推送
                    var unlockMsg = new RedisPushMsg
                    {
                        uid = 0,
                        gid = progressItem.gid,
                        title = "消息",
                        content = $"你发现了一个新的传送门...？",
                        type = "info",
                        show_type = 0
                    };
                    await RedisPublish.Publish(unlockMsg);
                }

                //回写
                progressItem.update_time = now;
                await progressDb.SimpleDb.AsUpdateable(progressItem).UpdateColumns(x => new { x.data, x.update_time }).ExecuteCommandAsync();
            }

            //更新
            RedisNumberCenter.MaxAutoUnlockGroup = newPgid;

            await response.OK();
        }

        [HttpHandler("POST", "/admin/unlock-next-puzzle-forall")]
        public async Task UnlockNextPuzzleForall(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Administrator);
            if (userSession == null) return;

            var requestJson = request.Json<UnlockNextPuzzleForallRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var pgid = requestJson.pgid;
            if (pgid <= 0 || pgid >= 6)
            {
                await response.BadRequest("只针对1~5区生效");
                return;
            }

            //刷新全员当前能量点
            //遍历所有progress
            var progressDb = DbFactory.Get<Progress>();
            var progressList = await progressDb.SimpleDb.AsQueryable().ToListAsync();

            var puzzleDb = DbFactory.Get<Puzzle>();
            var puzzleList = await puzzleDb.SimpleDb.AsQueryable().WithCache().ToListAsync();
            var now = DateTime.Now;

            foreach (var progressItem in progressList)
            {
                //为当前队伍指定分区解锁一题
                if (progressItem.data.UnlockedGroups.Contains(pgid))
                {
                    progressItem.data.UnlockNextPuzzle(pgid, puzzleList, now);
                }

                //回写
                progressItem.update_time = now;
                await progressDb.SimpleDb.AsUpdateable(progressItem).UpdateColumns(x => new { x.data, x.update_time }).ExecuteCommandAsync();
            }

            await response.OK();
        }
    }
}
