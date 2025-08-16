using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Ccxc.Core.HttpServer;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using ccxc_backend.Functions;
using ccxc_backend.Functions.PowerPoint;
using Newtonsoft.Json;
using SqlSugar;

namespace ccxc_backend.Controllers.Game
{
    [Export(typeof(HttpController))]
    public class GameInfoController : HttpController
    {
        [HttpHandler("POST", "/play/get-answer-log")]
        public async Task GetAnswerLog(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            var requestJson = request.Json<GetLastAnswerLogRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //取得该用户GID
            var groupBindItem = await ResponseExtend.GetUserGroupBind(userSession);
            if (groupBindItem == null)
            {
                await response.BadRequest("用户所属队伍不存在。");
                return;
            }

            var gid = groupBindItem.gid;

            //取得进度
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress == null)
            {
                await response.BadRequest("没有进度，请返回首页重新开始。");
                return;
            }

            var progressData = progress.data;
            if (progressData == null)
            {
                await response.BadRequest("未找到可用存档，请联系管理员。");
                return;
            }

            //取得答题历史
            var answerLogDb = DbFactory.Get<AnswerLog>();
            var answerList = await answerLogDb.SimpleDb.AsQueryable()
                .Where(it =>
                    it.gid == gid && it.pid == requestJson.pid && (it.status == 1 || it.status == 2 || it.status == 3 || it.status == 4))
                .OrderBy(it => it.create_time, OrderByType.Desc)
                .ToListAsync();

            //取得用户名缓存
            var teamUserName = await answerLogDb.Db.Queryable<user_group_bind, user>((ub, u) => new JoinQueryInfos(JoinType.Left, ub.uid == u.uid))
                .Where((ub, u) => ub.gid == gid)
                .Select((ub, u) => new {ub.uid, u.username})
                .WithCache("user")
                .ToListAsync();
            var userNameDict = new Dictionary<int, string>();
            if (teamUserName?.Count > 0)
            {
                userNameDict = teamUserName.ToDictionary(it => it.uid, it => it.username);
            }

            var resultList = answerList.Select(it => new AnswerLogView(it)
            {
                user_name = userNameDict.ContainsKey(it.uid) ? userNameDict[it.uid] : ""
            }).ToList();

            await response.JsonResponse(200, new GetLastAnswerLogResponse
            {
                status = 1,
                answer_log = resultList
            });
        }

        [HttpHandler("POST", "/play/get-tips")]
        public async Task GetTips(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            var requestJson = request.Json<GetPuzzleDetailRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //取得该用户GID
            var groupBindItem = await ResponseExtend.GetUserGroupBind(userSession);
            if (groupBindItem == null)
            {
                await response.BadRequest("用户所属队伍不存在。");
                return;
            }

            var gid = groupBindItem.gid;

            //取得进度
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress == null)
            {
                await response.BadRequest("没有进度，请返回首页重新开始。");
                return;
            }

            var progressData = progress.data;
            if (progressData == null)
            {
                await response.BadRequest("未找到可用存档，请联系管理员。");
                return;
            }

            var pid = requestJson.pid;
            //检查是否可见
            if (!progressData.UnlockedProblems.Contains(pid))
            {
                await response.BadRequest("无法找到请求的内容。");
                return;
            }


            //取得题目详情
            var puzzleDb = DbFactory.Get<Puzzle>();
            var puzzleItem = await puzzleDb.SimpleDb.AsQueryable().Where(x => x.pid == pid).FirstAsync();

            if (puzzleItem == null)
            {
                await response.Unauthorized("无法找到请求的内容。");
                return;
            }

            var unlockDelay = RedisNumberCenter.ManualTipReplyDelay;

            //提取整体解锁时间
            if (!progressData.ProblemUnlockTime.ContainsKey(puzzleItem.pid))
            {
                await response.Unauthorized("不能访问您未打开的区域");
                return;
            }
            var unlockTime = progressData.ProblemUnlockTime[puzzleItem.pid];

            var avaliableDelayMinute = RedisNumberCenter.UnlockTipFunctionAfter;
            var avaliableTime = unlockTime.AddMinutes(avaliableDelayMinute);

            //判断当前时间是否已经达到可见时间
            var now = DateTime.Now;
            if (now < avaliableTime)
            {
                //当前时间还未到可见时间，返回不可见
                await response.JsonResponse(200, new GetPuzzleTipsResponse
                {
                    status = 1,
                    is_tip_available = 0,
                    tip_available_time = avaliableTime,
                    tip_available_progress = 100.0 * (now - unlockTime).TotalMinutes / avaliableDelayMinute
                });
                return;
            }

            //提取本题提示信息
            var tipOpened = new HashSet<int>();
            if (progressData.OpenedHints.ContainsKey(puzzleItem.pid))
            {
                tipOpened = progressData.OpenedHints[puzzleItem.pid];
            }

            var puzzleTipsDb = DbFactory.Get<PuzzleTips>();
            var puzzleTipsList = await puzzleTipsDb.SimpleDb.AsQueryable().Where(x => x.pid == pid).ToListAsync();

            var puzzleTips = new List<PuzzleTipItem>();
            var oracleUnlockCost = RedisNumberCenter.DefaultOracleCost;
            var addAttemptsCountCost = RedisNumberCenter.AddAttemptsCountCost;             //购买额外尝试次数消费
            if (puzzleTipsList?.Count > 0)
            {
                puzzleTips = puzzleTipsList.Select(it => 
                {
                    if (it.desc == "oracle")
                    {
                        oracleUnlockCost = it.point_cost;
                        return null;
                    }

                    if (it.desc == "add_attempts_count")
                    {
                        addAttemptsCountCost = it.point_cost;
                        return null;
                    }

                    var r = new PuzzleTipItem
                    {
                        tips_id = it.ptid,
                        tip_num = it.order,
                    };

                    if (tipOpened.Contains(it.order))
                    {
                        r.is_avaliable = 1;
                        r.is_open = 1;
                        r.title = it.title;
                        r.content = it.content;
                        r.unlock_cost = it.point_cost;
                    }
                    else
                    {
                        r.is_open = 0;
                        r.tip_available_time = avaliableTime.AddMinutes(it.unlock_delay);
                        if (now < r.tip_available_time)
                        {
                            r.is_avaliable = 0;
                            r.tip_available_progress = 100.0 * (now - avaliableTime).TotalMinutes / it.unlock_delay;
                        }
                        else
                        {
                            r.is_avaliable = 1;
                            r.tip_available_progress = 100.0;
                            r.title = it.title;
                            r.unlock_cost = it.point_cost;
                        }
                    }

                    return r;
                }).Where(it => it != null).OrderBy(it => it.tip_num).ToList();
            }

            //提取人工提示信息
            var oracleDb = DbFactory.Get<DataModels.Oracle>();
            var oracleList = await oracleDb.SimpleDb.AsQueryable().Where(x => x.gid == gid && x.pid == requestJson.pid).OrderBy(x => x.create_time).ToListAsync();

            var oracleItem = oracleList.Select(it => new OracleSimpleItem
            {
                oracle_id = it.oracle_id,
                is_reply = it.is_reply,
                unlock_time = it.create_time.AddMinutes(unlockDelay)
            }).ToList();

            var res = new GetPuzzleTipsResponse
            {
                status = 1,
                is_tip_available = 1,
                tip_available_time = avaliableTime,
                tip_available_progress = 100.0,
                oracle_unlock_delay = unlockDelay,
                oracle_unlock_cost = oracleUnlockCost,
                add_attempts_count_cost = addAttemptsCountCost,
                puzzle_tips = puzzleTips,
                oracles = oracleItem
            };

            await response.JsonResponse(200, res);
        }

        [HttpHandler("POST", "/play/unlock-tips")]
        public async Task UnlockTips(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            var requestJson = request.Json<UnlockPuzzleTipRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //取得该用户GID
            var groupBindItem = await ResponseExtend.GetUserGroupBind(userSession);
            if (groupBindItem == null)
            {
                await response.BadRequest("用户所属队伍不存在。");
                return;
            }

            var gid = groupBindItem.gid;

            //取得进度
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress == null)
            {
                await response.BadRequest("没有进度，请返回首页重新开始。");
                return;
            }

            var progressData = progress.data;
            if (progressData == null)
            {
                await response.BadRequest("未找到可用存档，请联系管理员。");
                return;
            }

            var pid = requestJson.pid;
            //检查是否可见
            if (!progressData.UnlockedProblems.Contains(pid))
            {
                await response.BadRequest("无法找到请求的内容。");
                return;
            }
           


            //取得题目提示详情
            var puzzleDb = DbFactory.Get<PuzzleTips>();
            var puzzleTipsItem = await puzzleDb.SimpleDb.AsQueryable().Where(x => x.pid == pid && x.order == requestJson.tip_num).FirstAsync();

            if (puzzleTipsItem == null)
            {
                await response.Unauthorized("无法找到请求的内容。");
                return;
            }

            //提取解锁时间
            if (!progressData.ProblemUnlockTime.ContainsKey(pid))
            {
                await response.Unauthorized("不能访问您未打开的区域");
                return;
            }
            var unlockTime = progressData.ProblemUnlockTime[pid];

            var avaliableDelayMinute = RedisNumberCenter.UnlockTipFunctionAfter;
            var avaliableTime = unlockTime.AddMinutes(avaliableDelayMinute).AddMinutes(puzzleTipsItem.unlock_delay);

            //判断当前时间是否已经达到可见时间
            var now = DateTime.Now;
            if (now < avaliableTime)
            {
                //当前时间还未到可见时间，返回不可见
                await response.Unauthorized("分析未完成，不能提取");
                return;
            }

            //判断能量是否足够提取并扣减能量
            var isHintOpened = false;
            if (progressData.OpenedHints.ContainsKey(pid))
            {
                var openedHint = progressData.OpenedHints[pid];
                if (openedHint.Contains(requestJson.tip_num)) {
                    isHintOpened = true;
                }
            }

            if (isHintOpened)
            {
                await response.Unauthorized("您已经提取过该提示");
                return;
            }

            //未解锁，扣除能量点
            var currentPp = await PowerPoint.GetPowerPoint(progressDb, gid);
            var unlockTipCost = puzzleTipsItem.point_cost;

            if (currentPp < unlockTipCost)
            {
                await response.Forbidden("信用点不足");
                return;
            }
            await PowerPoint.UpdatePowerPoint(progressDb, gid, -unlockTipCost);

            //记录指定提示为open状态
            if (!progress.data.OpenedHints.ContainsKey(pid))
            {
                progress.data.OpenedHints.Add(pid, new HashSet<int>());
            }
            progress.data.OpenedHints[pid].Add(requestJson.tip_num);


            //写入日志
            var answerLogDb = DbFactory.Get<AnswerLog>();
            var answerLog = new answer_log
            {
                create_time = DateTime.Now,
                uid = userSession.uid,
                gid = gid,
                pid = pid,
                answer = $"[解锁提示 {requestJson.tip_num}: {puzzleTipsItem.title}]",
                status = 7
            };
            await answerLogDb.SimpleDb.AsInsertable(answerLog).ExecuteCommandAsync();

            //回写进度
            await progressDb.SimpleDb.AsUpdateable(progress).IgnoreColumns(it => new { it.finish_time, it.power_point, it.power_point_update_time }).ExecuteCommandAsync();

            //返回
            await response.OK();
        }

        [HttpHandler("POST", "/play/add-oracle")]
        public async Task AddOracle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            var requestJson = request.Json<GetPuzzleDetailRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //取得该用户GID
            var groupBindItem = await ResponseExtend.GetUserGroupBind(userSession);
            if (groupBindItem == null)
            {
                await response.BadRequest("用户所属队伍不存在。");
                return;
            }

            var gid = groupBindItem.gid;

            //取得进度
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress == null)
            {
                await response.BadRequest("没有进度，请返回首页重新开始。");
                return;
            }

            var progressData = progress.data;
            if (progressData == null)
            {
                await response.BadRequest("未找到可用存档，请联系管理员。");
                return;
            }

            var pid = requestJson.pid;
            //检查是否可见
            //如果pid在100~200，则检查UnlockedStarProblems，否则需要检查UnlockedProblems
            if (!progressData.UnlockedProblems.Contains(pid))
            {
                await response.BadRequest("无法找到请求的内容。");
                return;
            }


            //取得题目详情
            int unlockOracleCost = RedisNumberCenter.DefaultOracleCost; //解锁神谕消耗
            var puzzleTipsDb = DbFactory.Get<PuzzleTips>();
            var puzzleTipsItem = await puzzleTipsDb.SimpleDb.AsQueryable().Where(it => it.pid == requestJson.pid && it.desc == "oracle").WithCache().FirstAsync();
            if (puzzleTipsItem != null)
            {
                unlockOracleCost = puzzleTipsItem.point_cost;
            }

            //提取解锁时间
            if (!progressData.ProblemUnlockTime.ContainsKey(pid))
            {
                await response.Unauthorized("不能访问您未打开的区域");
                return;
            }
            var unlockTime = progressData.ProblemUnlockTime[pid];

            var avaliableDelayMinute = RedisNumberCenter.UnlockTipFunctionAfter;
            var avaliableTime = unlockTime.AddMinutes(avaliableDelayMinute);

            //判断当前时间是否已经达到可见时间
            var now = DateTime.Now;
            if (now < avaliableTime)
            {
                //当前时间还未到可见时间，返回不可见
                await response.Unauthorized("分析未完成，不能提取");
                return;
            }

            //判断能量是否足够提取并扣减能量
            var currentPp = await PowerPoint.GetPowerPoint(progressDb, gid);

            if (currentPp < unlockOracleCost)
            {
                await response.Forbidden("信用点不足");
                return;
            }
            await PowerPoint.UpdatePowerPoint(progressDb, gid, -unlockOracleCost);

            //添加Oracle数据库
            var oracleDb = DbFactory.Get<DataModels.Oracle>();
            var oracleItem = new oracle
            {
                gid = gid,
                pid = pid,
                update_time = now,
                create_time = now,
                is_reply = 0
            };
            await oracleDb.SimpleDb.AsInsertable(oracleItem).ExecuteCommandAsync();

            //返回
            await response.OK();
        }

        [HttpHandler("POST", "/play/open-oracle")]
        public async Task OpenOracle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            var requestJson = request.Json<OpenOracleRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //取得该用户GID
            var groupBindItem = await ResponseExtend.GetUserGroupBind(userSession);
            if (groupBindItem == null)
            {
                await response.BadRequest("用户所属队伍不存在。");
                return;
            }

            var gid = groupBindItem.gid;

            //取得进度
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress == null)
            {
                await response.BadRequest("没有进度，请返回首页重新开始。");
                return;
            }

            var progressData = progress.data;
            if (progressData == null)
            {
                await response.BadRequest("未找到可用存档，请联系管理员。");
                return;
            }

            var oracleDb = DbFactory.Get<DataModels.Oracle>();
            var oracleItem = await oracleDb.SimpleDb.AsQueryable().Where(x => x.gid == gid && x.oracle_id == requestJson.oracle_id).FirstAsync();
            if (oracleItem == null)
            {
                await response.BadRequest("未找到该Oracle");
                return;
            }

            //时间未到开放时间，则不返回回复内容
            var unlockDelay = RedisNumberCenter.ManualTipReplyDelay;
            var unlockTime = oracleItem.create_time.AddMinutes(unlockDelay);
            oracleItem.unlock_time = unlockTime;
            if (DateTime.Now < unlockTime)
            {
                oracleItem.reply_content = "";
            }
            else
            {
                //开放回复内容，此时可能同时有一些提示被后台手工打开，给它设置提示
                if (!string.IsNullOrEmpty(oracleItem.extend_function))
                {
                    var openTips = oracleItem.extend_function.Split(',').Select(it => int.Parse(it)).ToList();
                    var changed = false;
                    foreach (var tip in openTips)
                    {
                        if (!progress.data.OpenedHints.ContainsKey(oracleItem.pid))
                        {
                            progress.data.OpenedHints[oracleItem.pid] = new HashSet<int>();
                        }
                        if (!progress.data.OpenedHints[oracleItem.pid].Contains(tip))
                        {
                            progress.data.OpenedHints[oracleItem.pid].Add(tip);
                            changed = true;
                        }
                    }

                    //如果有更新，则回写存档
                    if (changed)
                    {
                        await progressDb.SimpleDb.AsUpdateable(progress).IgnoreColumns(x => new { x.finish_time, x.power_point, x.power_point_update_time }).ExecuteCommandAsync();
                    }
                }
            }

            await response.JsonResponse(200, new OpenOracleResponse
            {
                status = 1,
                data = oracleItem
            });
        }

        [HttpHandler("POST", "/play/edit-oracle")]
        public async Task EditOracle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            var requestJson = request.Json<EditOracleRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //取得该用户GID
            var groupBindItem = await ResponseExtend.GetUserGroupBind(userSession);
            if (groupBindItem == null)
            {
                await response.BadRequest("用户所属队伍不存在。");
                return;
            }

            var gid = groupBindItem.gid;

            //取得进度
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress == null)
            {
                await response.BadRequest("没有进度，请返回首页重新开始。");
                return;
            }

            var progressData = progress.data;
            if (progressData == null)
            {
                await response.BadRequest("未找到可用存档，请联系管理员。");
                return;
            }

            var oracleDb = DbFactory.Get<DataModels.Oracle>();
            var oracleItem = await oracleDb.SimpleDb.AsQueryable().Where(x => x.gid == gid && x.oracle_id == requestJson.oracle_id).FirstAsync();
            if (oracleItem == null)
            {
                await response.BadRequest("未找到该Oracle");
                return;
            }

            //执行更新
            oracleItem.question_content = requestJson.question_content;
            oracleItem.update_time = DateTime.Now;
            await oracleDb.SimpleDb.AsUpdateable(oracleItem).UpdateColumns(x => new { x.question_content, x.update_time }).ExecuteCommandAsync();

            await response.JsonResponse(200, new OpenOracleResponse
            {
                status = 1,
                data = oracleItem
            });
        }

        [HttpHandler("POST", "/play/add-attempts-count")]
        public async Task AddAttemptsCount(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            var requestJson = request.Json<UnlockPuzzleTipRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //取得该用户GID
            var groupBindItem = await ResponseExtend.GetUserGroupBind(userSession);
            if (groupBindItem == null)
            {
                await response.BadRequest("用户所属队伍不存在。");
                return;
            }

            var gid = groupBindItem.gid;

            //取得进度
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress == null)
            {
                await response.BadRequest("没有进度，请返回首页重新开始。");
                return;
            }

            var progressData = progress.data;
            if (progressData == null)
            {
                await response.BadRequest("未找到可用存档，请联系管理员。");
                return;
            }

            var pid = requestJson.pid;
            //检查是否可见
            if (!progressData.UnlockedProblems.Contains(pid))
            {
                await response.BadRequest("无法找到请求的内容。");
                return;
            }

               

            //读入题目
            var puzzleDb = DbFactory.Get<Puzzle>();
            var puzzleItem = await puzzleDb.SimpleDb.AsQueryable().Where(x => x.pid == pid).FirstAsync();
            if (puzzleItem == null)
            {
                await response.BadRequest("无法找到请求的内容。(404)");
                return;
            }

            //取得增加提交次数的消耗
            var addAttemptsCountCost = RedisNumberCenter.AddAttemptsCountCost;
            var puzzleTipsDb = DbFactory.Get<PuzzleTips>();
            var puzzleTipsItem = await puzzleTipsDb.SimpleDb.AsQueryable().Where(it => it.pid == requestJson.pid && it.desc == "add_attempts_count").WithCache().FirstAsync();
            if (puzzleTipsItem != null)
            {
                addAttemptsCountCost = puzzleTipsItem.point_cost;
            }


            //判断能量是否足够提取并扣减能量
            var currentPp = await PowerPoint.GetPowerPoint(progressDb, gid);
            if (currentPp < addAttemptsCountCost)
            {
                await response.Forbidden("信用点不足");
                return;
            }
            await PowerPoint.UpdatePowerPoint(progressDb, gid, -addAttemptsCountCost);

            //给指定pid的题目增加提交次数
            if (!progress.data.AdditionalProblemAttemptsCount.ContainsKey(pid))
            {
                progress.data.AdditionalProblemAttemptsCount.Add(pid, 0);
            }
            progress.data.AdditionalProblemAttemptsCount[pid] += puzzleItem.attempts_count;


            //写入日志
            var answerLogDb = DbFactory.Get<AnswerLog>();
            var answerLog = new answer_log
            {
                create_time = DateTime.Now,
                uid = userSession.uid,
                gid = gid,
                pid = pid,
                answer = $"[购买提交次数]",
                status = 7
            };
            await answerLogDb.SimpleDb.AsInsertable(answerLog).ExecuteCommandAsync();

            //回写进度
            await progressDb.SimpleDb.AsUpdateable(progress).IgnoreColumns(it => new { it.finish_time, it.power_point, it.power_point_update_time }).ExecuteCommandAsync();

            //返回
            await response.OK();
        }

        [HttpHandler("POST", "/play/get-puzzle-board")]
        public async Task GetPuzzlesBoard(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            var requestJson = request.Json<GetPuzzleListRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //取得该用户GID
            var groupBindItem = await ResponseExtend.GetUserGroupBind(userSession);
            if (groupBindItem == null)
            {
                await response.BadRequest("用户所属队伍不存在。");
                return;
            }

            var gid = groupBindItem.gid;

            //取得进度
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress == null)
            {
                await response.BadRequest("没有进度，请返回首页重新开始。");
                return;
            }

            var progressData = progress.data;
            if (progressData == null)
            {
                await response.BadRequest("未找到可用存档，请联系管理员。");
                return;
            }

            //从Redis缓存中取得统计结果
            var now = DateTime.Now;
            var cache = DbFactory.GetCache();

            var puzzleBoardStatKey = cache.GetCacheKey("puzzle_board_stat_cache");
            var cacheData = await cache.Get<PuzzleStatCache>(puzzleBoardStatKey);

            var resultData = new List<PuzzleStat>();
            var cacheTime = now;
            if (cacheData != null && cacheData.cache_time > now.AddMinutes(-1))
            {
                resultData = cacheData.data;
                cacheTime = cacheData.cache_time;
            }
            else
            {
                //无缓存或缓存过期，从数据库中读取
                var puzzleDb = DbFactory.Get<Puzzle>();
                var puzzleList = await puzzleDb.SimpleDb.AsQueryable().OrderBy(x => x.pid).WithCache().ToListAsync();

                var groupDb = DbFactory.Get<UserGroup>();
                var groupIdList = await groupDb.SimpleDb.AsQueryable().Where(x => x.is_hide != 1).Select(x => x.gid).ToListAsync();
                var groupIdSet = new HashSet<int>(groupIdList);

                //各队信息
                var progressAllList = await progressDb.SimpleDb.AsQueryable().ToListAsync();
                var progressList = progressAllList.Where(it => groupIdSet.Contains(it.gid)).ToList();

                var unlockCountDict = new Dictionary<int, int>();
                var finishCountDict = new Dictionary<int, int>();

                foreach (var p in progressList)
                {
                    var pd = p.data;
                    if (pd == null) continue;
                    foreach (var up in pd.UnlockedProblems)
                    {
                        if (!unlockCountDict.ContainsKey(up))
                        {
                            unlockCountDict.Add(up, 0);
                        }
                        unlockCountDict[up]++;
                    }
                    foreach (var fp in pd.FinishedProblems)
                    {
                        if (!finishCountDict.ContainsKey(fp))
                        {
                            finishCountDict.Add(fp, 0);
                        }
                        finishCountDict[fp]++;
                    }
                }

                //读取各题目最后一次回答的用户名
                var lastAnswerUserListQuery = puzzleDb.Db.Queryable<answer_log, user>((a, u) => new JoinQueryInfos(JoinType.Left, a.uid == u.uid))
                    .Where((a, u) => a.status == 1)
                    .Select((a, u) => new
                    {
                        rn = SqlFunc.RowNumber($"{a.create_time} desc", a.pid),
                        pid = a.pid,
                        uid = a.uid,
                        username = u.username
                    }).MergeTable().Where(x => x.rn == 1);

                var lastAnswerSql = lastAnswerUserListQuery.ToSql();

                Ccxc.Core.Utils.Logger.Info($"Running SQL: {lastAnswerSql.Key}");
                Ccxc.Core.Utils.Logger.Info($"SQL: {string.Join("; ", lastAnswerSql.Value.Select(v => $"{v.ParameterName} = {v.Value}"))}");

                var lastAnswerUserList = await lastAnswerUserListQuery.ToListAsync();
                var lastAnswerDict = lastAnswerUserList.ToDictionary(x => x.pid, x => x.username);

                resultData = puzzleList.Select(it =>
                {
                    var stat = new PuzzleStat
                    {
                        pid = it.pid,
                        pgid = it.pgid,
                        title = it.title,
                        unlock_count = 0,
                        finish_count = 0,
                        last_answer_person_name = "",
                    };

                    if (unlockCountDict.ContainsKey(it.pid))
                    {
                        stat.unlock_count = unlockCountDict[it.pid];
                    }
                    if (finishCountDict.ContainsKey(it.pid))
                    {
                        stat.finish_count = finishCountDict[it.pid];
                    }
                    if (lastAnswerDict.ContainsKey(it.pid))
                    {
                        stat.last_answer_person_name = lastAnswerDict[it.pid];
                    }

                    return stat;
                }).ToList();

                //写入缓存
                var cacheDataNew = new PuzzleStatCache
                {
                    data = resultData,
                    cache_time = now
                };
                await cache.Put(puzzleBoardStatKey, cacheDataNew, 65000);
            }

            //从resultData中过滤出当前用户已解锁的题目
            var insertListResultData = new List<PuzzleStat>();
            var unlockedPuzzleList = new List<PuzzleStat>();
            if (requestJson.pgid != 0)
            {
                insertListResultData = resultData.Where(x => x.pgid == requestJson.pgid).ToList();
            }

            foreach (var p in insertListResultData)
            {
                if (progressData.UnlockedProblems.Contains(p.pid))
                {
                    unlockedPuzzleList.Add(p);
                }
            }

            await response.JsonResponse(200, new GetPuzzleBoardResponse
            {
                status = 1,
                data = unlockedPuzzleList,
                cache_time = cacheTime
            });
        }

        [HttpHandler("POST", "/play/get-library")]
        public async Task GetLibrary(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            //取得该用户GID
            var groupBindItem = await ResponseExtend.GetUserGroupBind(userSession);
            if (groupBindItem == null)
            {
                await response.BadRequest("用户所属队伍不存在。");
                return;
            }

            var gid = groupBindItem.gid;

            //取得进度
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress == null)
            {
                await response.BadRequest("没有进度，请返回首页重新开始。");
                return;
            }

            var progressData = progress.data;
            if (progressData == null)
            {
                await response.BadRequest("未找到可用存档，请联系管理员。");
                return;
            }

            //用户可见的剧情ID
            var libraryIds = progressData.GetOpenPuzzleArticleId();

            //取得文章数据
            var puzzleArticleDb = DbFactory.Get<PuzzleArticle>();
            var articleList = await puzzleArticleDb.SimpleDb.AsQueryable()
                .Where(it => libraryIds.Contains(it.key)).Select(it => new { it.key, it.title }).WithCache().ToListAsync();

            var articleDict = articleList.ToDictionary(it => it.key, it => it.title);

            //整理数据
            var storyData = libraryIds.Select(it =>
            {
                var title = articleDict.ContainsKey(it) ? articleDict[it] : "【未命名文章】";
                return new StoryItem
                {
                    key = it,
                    title = title
                };
            }).ToList();

            await response.JsonResponse(200, new GetLibraryResponse
            {
                status = 1,
                data = storyData
            });
        }

        [HttpHandler("POST", "/play/get-puzzle-analysis")]
        public async Task GetPuzzleAnalysis(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            var requestJson = request.Json<GetPuzzleDetailRequest>();

            if (Config.SystemConfigLoader.Config.ShowAnalysis == 0)
            {
                if (userSession.roleid < 4)
                {
                    await response.BadRequest("题目解析查看功能未开放。");
                    return;
                }
            }

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //取得该用户GID
            var groupBindItem = await ResponseExtend.GetUserGroupBind(userSession);
            if (groupBindItem == null)
            {
                await response.BadRequest("用户所属队伍不存在。");
                return;
            }

            var gid = groupBindItem.gid;

            //取得进度
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress == null)
            {
                await response.BadRequest("没有进度，请返回首页重新开始。");
                return;
            }

            var progressData = progress.data;
            if (progressData == null)
            {
                await response.BadRequest("未找到可用存档，请联系管理员。");
                return;
            }

            //提取指定题目的解析
            var puzzleDb = DbFactory.Get<Puzzle>();
            var puzzleItem = await puzzleDb.SimpleDb.AsQueryable().Where(x => x.pid == requestJson.pid).FirstAsync();
            if (puzzleItem == null)
            {
                await response.BadRequest("未找到指定题目。");
                return;
            }

            await response.JsonResponse(200, new GetPuzzleAnalysisResponse
            {
                status = 1,
                title = puzzleItem.title,
                author = puzzleItem.author,
                answer = puzzleItem.answer,
                analysis = puzzleItem.analysis,
                check_answer_type = puzzleItem.check_answer_type,
            });
        }
    }
}
