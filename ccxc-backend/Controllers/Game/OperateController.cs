using Ccxc.Core.HttpServer;
using Ccxc.Core.Plugins.DataModels;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using ccxc_backend.Functions;
using ccxc_backend.Functions.JavaScriptEngine;
using ccxc_backend.Functions.PowerPoint;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp.Primitives;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Game
{
    [Export(typeof(HttpController))]
    public class OperateController : HttpController
    {
        [HttpHandler("POST", "/check-answer")]
        public async Task CheckAnswer(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            var requestJson = request.Json<CheckAnswerRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var now = DateTime.Now;
            var answerLogDb = DbFactory.Get<AnswerLog>();
            var answerLog = new answer_log
            {
                create_time = now,
                uid = userSession.uid,
                pid = requestJson.pid,
                answer = requestJson.answer
            };

            //忽略答案中的空格、制表符和减号
            var lowerCaseAnswer = requestJson.answer.ToLowerInvariant();
            var answer = Regex.Replace(lowerCaseAnswer, @"[\s\u200B\u200C\uFEFF-]", ""); // 使用正则表达式替换空格、制表符和减号

            //取得该用户GID
            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == userSession.uid).FirstAsync();

            if (groupBindItem == null)
            {
                answerLog.status = 5;
                await answerLogDb.SimpleDb.AsInsertable(answerLog).ExecuteCommandAsync();

                await response.BadRequest("未确定组队？");
                return;
            }

            var gid = groupBindItem.gid;
            answerLog.gid = gid;

            //取得进度
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress == null)
            {
                answerLog.status = 5;
                await answerLogDb.SimpleDb.AsInsertable(answerLog).ExecuteCommandAsync();

                await response.BadRequest("没有进度，请返回首页重新开始。");
                return;
            }

            var progressData = progress.data;
            if (progressData == null)
            {
                answerLog.status = 5;
                await answerLogDb.SimpleDb.AsInsertable(answerLog).ExecuteCommandAsync();

                await response.BadRequest("未找到可用存档，请联系管理员。");
                return;
            }

            //取出待判定题目
            var puzzleDb = DbFactory.Get<Puzzle>();
            var puzzleList = await puzzleDb.SimpleDb.AsQueryable().WithCache().ToListAsync();

            /**
             * 判题流程
             * 1. 判断题目是否可见
             *   -- 不可见 -> 返回错误提示（注销登录并跳转回首页）
             *   -- 可见 -> 下一步
             * 2. 判断剩余答题次数是否足够
             *   -- 不够 -> 返回提示
             *   -- 足够 -> 下一步
             * 3. 判断答案是否正确
             *   -- 不正确 -> {
             *      3.1. 判断附加提示答案中是否存在此答案
             *        -- 存在 -> {
             *           判断附加提示是否存在extras，若存在，对extras进行处理
             *           返回答案错误+附加提示，并标记刷新当前页
             *        }
             *        -- 不存在 -> {
             *           记录错误次数
             *           返回答案错误
             *        }
             *      }
             *   -- 正确 -> 下一步
             * 4. 判断该题是否为初次回答正确
             *   -- 不是初次回答正确 -> 返回回答正确
             *   -- 初次回答正确 -> 标记此题回答正确，然后下一步
             * 5. 判断是否为首杀
             *   -- 是首杀 -> 写入首杀数据库，然后下一步
             *   -- 不是首杀 -> 下一步
             * 6. 判断是否为FinalMeta
             *   -- 是FinalMeta -> {
             *      6.1. 判断是否已经完赛
             *        -- 已经完赛 -> 标记跳转到finalend，然后返回
             *        -- 未完赛 -> 标记完赛，然后标记跳转到finalend，然后返回
             *      }
             *   -- 不是 -> 下一步
             * 7. 判断是否为MM // CCBC 16 没有MM，这一段忽略
             *   -- 是MM -> 标记解锁FinalMeta，并记录解锁时间，然后下一步
             *   -- 不是MM -> 下一步
             * 8. 判断是否为分区Meta
             *   -- 是 -> {
             *      //以下逻辑均为 CCBC 16 的特殊逻辑
             *      8.1 通过指南、印刷、火药区时(pgid=2,3,4)，给下一分区解锁一题，并记录相应的解锁时间，然后转到8.2
             *      8.2 判断是否2～5区所有分区Meta都已完成
             *        -- 已完成 -> 标记终章解锁，同时解锁区域内的所有小题和Final Meta，并记录相应的解锁时间，然后下一步
             *        -- 否则 -> 下一步
             *      }
             *   -- 不是 -> 下一步
             * 9. 判断当前区域小题是否已达到Meta解锁条件。
             *   -- 已达到 -> {
             *      9.1 标记当前区域的Meta解锁，并记录相应的解锁时间，然后下一步
             *      }
             *   -- 没有 -> 下一步
             * 10. 判断当前区域小题是否已达到下个区域解锁条件。
             *   -- 已达到 -> {
             *      10.1 判断是否还存在下一个区域（pgid=6，终章除外）
             *        -- 存在 -> 标记解锁下一个区域和区域内的初始小题，并记录相应的解锁时间，然后转到下一步
             *        -- 不存在 -> 转到下一步
             *      }
             *   -- 没有 -> 下一步
             * 10. 判断是否当前区域有题可以解锁，如果可以，则解锁一题，并记录相应的解锁时间，然后下一步
             * 11. 检查是否当前题目有扩展内容
             *   -- 有扩展内容 -> 返回标记刷新当前页
             *   -- 没有扩展内容 -> 返回
             */
            var pushMsg = new RedisPushMsg
            {
                uid = userSession.uid,
                gid = gid,
                title = "消息"
            };

            //1. 判定题目可见性
            var puzzleItem = puzzleList.Where(it => it.pid == requestJson.pid).FirstOrDefault();

            if (puzzleItem == null)
            {
               answerLog.status = 6;
               await answerLogDb.SimpleDb.AsInsertable(answerLog).ExecuteCommandAsync();

               await response.BadRequest("题目不存在或未解锁。");
               return;
            }

            var pid = requestJson.pid;
            
            if (!progressData.UnlockedProblems.Contains(pid))
            {
                answerLog.status = 6;
                await answerLogDb.SimpleDb.AsInsertable(answerLog).ExecuteCommandAsync();

                await response.BadRequest("题目不存在或未解锁。");
                return;
            }

            //2. 判断剩余答题次数是否足够
            var attemptsCount = progressData.ProblemAnswerSubmissionsCount.ContainsKey(pid) ? progressData.ProblemAnswerSubmissionsCount[pid] : 0;
            var attemptsTotal = puzzleItem.attempts_count + (progressData.AdditionalProblemAttemptsCount.ContainsKey(pid) ? progressData.AdditionalProblemAttemptsCount[pid] : 0);
            if (attemptsCount >= attemptsTotal)
            {
                answerLog.status = 3;
                await answerLogDb.SimpleDb.AsInsertable(answerLog).ExecuteCommandAsync();

                await response.BadRequest("尝试次数已用尽。");
                return;
            }

            //3. 判断答案是否正确
            var extendFlag = 0;
            string extendJumpLocation = "";

            var isCorrect = false; //判题结果
            var hitMileStone = false; //是否击中里程碑
            string extraMessage = null; //附加提示信息
            if (puzzleItem.check_answer_type == 0)
            {
                //标准判题函数
                var trueAnswer = puzzleItem.answer.ToLower().Replace(" ", "").Replace("-", "");
                isCorrect = string.Equals(trueAnswer, answer, StringComparison.CurrentCultureIgnoreCase);
            }
            else
            {
                if (string.IsNullOrEmpty(puzzleItem.check_answer_function))
                {
                    answerLog.status = 5;
                    await answerLogDb.SimpleDb.AsInsertable(answerLog).ExecuteCommandAsync();
                    await response.BadRequest("题目判题函数不存在。(S1)");
                    return;
                }

                //取出判题脚本
                var scriptDb = DbFactory.Get<PuzzleBackendScript>();
                var scriptItem = await scriptDb.SimpleDb.AsQueryable().Where(it => it.key == puzzleItem.check_answer_function).WithCache().FirstAsync();
                if (scriptItem == null)
                {
                    answerLog.status = 5;
                    await answerLogDb.SimpleDb.AsInsertable(answerLog).ExecuteCommandAsync();
                    await response.BadRequest("题目判题函数不存在。(S2)");
                    return;
                }

                //准备注入JS引擎的环境
                var script = scriptItem.script;
                var jsEngine = new Jurassic.ScriptEngine();
                var checkAnswerContext = new CheckAnswerScriptContext(jsEngine, requestJson.answer, answer, userSession, gid, pid, progress.data.ProblemStatus);
                jsEngine.SetGlobalValue("ctx", checkAnswerContext);
                jsEngine.Execute(script);

                //判题时会强制重写data，所以不需要独立回写

                //将结果取出
                isCorrect = checkAnswerContext.result;
                hitMileStone = checkAnswerContext.isHitMilestone;
                extraMessage = checkAnswerContext.extraMessage;
            }


            if (!isCorrect)
            {
                //答案错误，判断是否命中里程碑
                var addAnswerDb = DbFactory.Get<AdditionalAnswer>();
                var addAnswerListAll = await addAnswerDb.SimpleDb.AsQueryable().Where(x => x.pid == puzzleItem.pid).WithCache().ToListAsync();
                var regexAddAnswerList = addAnswerListAll.Where(x => x.answer.StartsWith("@/")).ToList();
                var addAnswerDict = addAnswerListAll.Where(x => !x.answer.StartsWith("@/")).ToDictionary(x => x.answer.ToLower().Replace(" ", "").Replace("-", ""), x => x);

                var message = "回答错误";

                if (!string.IsNullOrEmpty(extraMessage))
                {
                    message = $"回答错误，但你获得了一些信息：{extraMessage}";
                    answerLog.message = extraMessage;
                }

                pushMsg.content = $"{userSession.username} 在 {puzzleItem.title} 回答错误。";
                pushMsg.type = "danger";
                pushMsg.show_type = 1;

                //检查是否命中里程碑
                string milestoneMessage = null;
                string milestoneExtraCommand = null;
                if (hitMileStone)
                {
                    //如果已经命中里程碑，则说明是判题脚本设置的，此时跳过标准流程的里程碑检查
                    milestoneMessage = extraMessage;
                }
                else
                {
                    additional_answer addAnswerItem = null;
                    if (addAnswerDict.ContainsKey(answer))
                    {
                        hitMileStone = true;
                        addAnswerItem = addAnswerDict[answer];
                    }
                    else
                    {
                        //检查所有的regex模式并逐一匹配
                        foreach (var regAddAnswerItem in regexAddAnswerList)
                        {
                            var reg = new Regex(regAddAnswerItem.answer[2..]);
                            if (reg.IsMatch(requestJson.answer))
                            {
                                hitMileStone = true;
                                addAnswerItem = regAddAnswerItem;
                                break;
                            }
                        }
                    }

                    milestoneMessage = addAnswerItem?.message;
                    milestoneExtraCommand = addAnswerItem?.extra;
                }

                if (hitMileStone)
                {
                    message = $"你获得了一些信息：{milestoneMessage}";

                    pushMsg.content = $"{userSession.username} 在 {puzzleItem.title} 到达里程碑！";
                    pushMsg.type = "warning";

                    answerLog.status = 4;
                    answerLog.message = milestoneMessage;

                    //如果存在extra，则需要解析extra并对题目存档状态进行修改
                    if (!string.IsNullOrEmpty(milestoneExtraCommand))
                    {
                        extendFlag = 16; //存在extra时需要刷新题目
                        var extraCommands = milestoneExtraCommand.Split(' ');
                        //extra命令例子：
                        //set key value
                        //del key
                        //clear
                        if (extraCommands?.Length > 0)
                        {
                            var command = extraCommands[0];
                            if (command == "set")
                            {
                                if (extraCommands.Length < 2)
                                {
                                    await response.InternalServerError("extra命令格式错误。");
                                    return;
                                }

                                var key = extraCommands[1];
                                var value = "";
                                if (extraCommands.Length > 2)
                                {
                                    value = extraCommands[2];
                                }

                                if (!progress.data.ProblemStatus.ContainsKey(pid))
                                {
                                    progress.data.ProblemStatus.Add(pid, new Dictionary<string, string>());
                                }
                                if (!progress.data.ProblemStatus[pid].ContainsKey(key))
                                {
                                    progress.data.ProblemStatus[pid].Add(key, value);
                                }
                                else
                                {
                                    progress.data.ProblemStatus[pid][key] = value;
                                }
                            }
                            else if (command == "del")
                            {
                                if (extraCommands.Length < 2)
                                {
                                    await response.InternalServerError("extra命令格式错误。");
                                    return;
                                }

                                var key = extraCommands[1];

                                if (!progress.data.ProblemStatus.ContainsKey(pid))
                                {
                                    progress.data.ProblemStatus.Add(pid, new Dictionary<string, string>());
                                }
                                if (progress.data.ProblemStatus[pid].ContainsKey(key))
                                {
                                    progress.data.ProblemStatus[pid].Remove(key);
                                }
                            }
                            else if (command == "clear")
                            {
                                if (!progress.data.ProblemStatus.ContainsKey(pid))
                                {
                                    progress.data.ProblemStatus.Add(pid, new Dictionary<string, string>());
                                }
                                progress.data.ProblemStatus[pid].Clear();
                            }
                        }
                    }
                }
                else
                {
                    //答案未命中里程碑，记录错误次数
                    answerLog.status = 2;

                    if (progress.data.ProblemAnswerSubmissionsCount.ContainsKey(pid))
                    {
                        progress.data.ProblemAnswerSubmissionsCount[pid] += 1;
                    }
                    else
                    {
                        progress.data.ProblemAnswerSubmissionsCount.Add(pid, 1);
                    }
                }

                //发送推送
                await RedisPublish.Publish(pushMsg);

                //更新存档
                await progressDb.SimpleDb.AsUpdateable(progress).IgnoreColumns(it => new { it.finish_time, it.power_point, it.power_point_update_time }).ExecuteCommandAsync();
                await answerLogDb.SimpleDb.AsInsertable(answerLog).ExecuteCommandAsync();

                await response.JsonResponse(406, new AnswerResponse //使用 406 Not Acceptable 作为答案错误的专用返回码。若返回 200 OK 则为答案正确
                {
                    status = 1,
                    answer_status = answerLog.status,
                    message = message,
                    extend_flag = extendFlag
                });
                return;
            }

            //答案正确，完成进度推进逻辑
            var resultTuple = await PushNextHelper(
                progress, progressDb,
                puzzleItem, puzzleList, answerLog, answerLogDb,
                userSession.uid, gid, userSession.username,
                now, extraMessage
            );

            var resultCode = 200;
            if (resultTuple.code == 0)
            {
                resultCode = 400;
            }

            var resultResponse = new AnswerResponse
            {
                status = resultTuple.code == 1 ? 1 : 2,
                answer_status = resultTuple.answerStatus,
                extend_flag = resultTuple.extendFlag,
                message = resultTuple.message,
                location = resultTuple.location
            };


            await response.JsonResponse(resultCode, resultResponse);
        }

        public static async Task<(int code, int answerStatus, int extendFlag, string message, string location)> PushNextHelper(
            progress progress, Progress progressDb,
            puzzle puzzleItem, List<puzzle> puzzleList, answer_log answerLog, AnswerLog answerLogDb,
            int uid, int gid, string username,
            DateTime now, string extraMessage
            )
        {
            answerLog.status = 1;

            var pushMsg = new RedisPushMsg
            {
                uid = uid,
                gid = gid,
                title = "消息"
            };

            //4. 判断是否为初次回答正确
            var successMessage = "恭喜你，回答正确！";
            if (!progress.data.FinishedProblems.Contains(puzzleItem.pid))
            {
                //初次回答正确逻辑，查看并解锁后续内容
                pushMsg.content = $"{username} 在 {puzzleItem.title} 回答正确！";
                pushMsg.type = "success";
                pushMsg.show_type = 1;

                //标记此题回答正确
                progress.data.FinishedProblems.Add(puzzleItem.pid);

                //5. 判断是否为首杀
                //计算是否为首杀
                var tempAnnoDb = DbFactory.Get<TempAnno>();
                var c = await tempAnnoDb.SimpleDb.AsQueryable().Where(x => x.pid == puzzleItem.pid).CountAsync();
                if (c == 0)
                {
                    //记录首杀
                    successMessage += "！！！你达成了本题首解！";
                    answerLog.message = "达成本题首解！";
                    var newTempAnno = new temp_anno
                    {
                        pid = puzzleItem.pid,
                        first_solver_gid = gid,
                        first_solve_time = now
                    };

                    try
                    {
                        await tempAnnoDb.SimpleDb.AsInsertable(newTempAnno).ExecuteCommandAsync();
                    }
                    catch (Exception e)
                    {
                        Ccxc.Core.Utils.Logger.Error($"首杀数据写入失败，原因可能是：{e.Message}，附完整数据：{JsonConvert.SerializeObject(newTempAnno)}，详细信息：" + e.ToString());
                        //写入不成功可能是产生了竞争或者主键已存在。总之这里忽略掉这个异常。
                    }
                }

                //6. 判断是否为FinalMeta
                if (puzzleItem.answer_type == 3)
                {
                    //如果未标记为初次完赛则设定完赛状态
                    if (progress.is_finish == 0)
                    {
                        progress.is_finish = 1;
                        progress.finish_time = now;
                        progress.data.UnlockedProblems.Add(60);
                    }
                }
                // CCBC 16 没有 MM ，这一段跳过
                /*
                //7. 判断是否为MM
                else if (puzzleItem.answer_type == 2)
                {
                    //标记FinalMeta解锁
                    //先获取FM的pid
                    var FMPid = GameProgressExtend.GetFMPid;

                    //解锁FM
                    if (!progress.data.UnlockedProblems.Contains(FMPid))
                    {

                        progress.data.UnlockedProblems.Add(FMPid);
                        progress.data.ProblemUnlockTime.Add(FMPid, now);
                        //给前端推送Meta解锁信息
                        var unlockMsg = new RedisPushMsg
                        {
                            uid = userSession.uid,
                            gid = gid,
                            title = "消息",
                            content = $"水晶球告诉你，你还有最后一件事要做。",
                            type = "info",
                            show_type = 0
                        };
                        await RedisPublish.Publish(unlockMsg);
                    }
                }*/
                else
                {
                    //8. 判断是否为分区Meta
                    if (puzzleItem.answer_type == 1)
                    {
                        var currentPgid = puzzleItem.pgid;
                        //首先标记区域完成
                        progress.data.FinishedGroups.Add(puzzleItem.pgid);

                        //8.1 通过Meta时，给下一分区解锁一题，并记录相应的解锁时间
                        //（CCBC16 特殊规则：新手区除外，终章区除外）
                        
                        //if (currentPgid >= 2 && currentPgid <= 4)
                        //{
                        //    var nextPgid = currentPgid + 1;
                        //    if (progress.data.UnlockedGroups.Contains(nextPgid))
                        //    {
                        //        progress.data.UnlockNextPuzzle(nextPgid, puzzleList, now);
                        //    }
                        //}

                        //8.2 新手区完成时解锁本篇
                        if (currentPgid == 1) //新手区完成时，解锁2区，然后根据自动解锁上限，解锁后续的区域。
                        {
                            progress.data.UnlockGroup(2, puzzleList, now);

                            var unlockGroupCount = 1;
                            //如果当前已经手工打开3～5区，则也同时解锁
                            var maxAutoUnlockPgid = RedisNumberCenter.MaxAutoUnlockGroup;
                            for (var i = 3; i <= maxAutoUnlockPgid; i++)
                            {
                                progress.data.UnlockGroup(i, puzzleList, now);
                                unlockGroupCount++;
                            }

                            if (unlockGroupCount > 0)
                            {
                                for (var ugci = 0; ugci < unlockGroupCount; ugci++)
                                {
                                    //给前端推送区域解锁信息
                                    var unlockMsg = new RedisPushMsg
                                    {
                                        uid = uid,
                                        gid = gid,
                                        title = "消息",
                                        content = $"飞船接收到了一个新的区域坐标...？",
                                        type = "info",
                                        show_type = 0
                                    };
                                    await RedisPublish.Publish(unlockMsg);
                                }
                            }
                        }

                        //8.3 判断是否全部分区Meta都已完成
                        if (progress.data.FinishedGroups.Count >= 5)
                        {
                            //CCBC16 特殊逻辑：解锁终章（pgid=6）
                            progress.data.UnlockGroup(6, puzzleList, now);

                            //同时解锁FinalMeta
                            var FMPid = GameProgressExtend.GetFMPid;
                            if (!progress.data.UnlockedProblems.Contains(FMPid))
                            {
                                progress.data.UnlockedProblems.Add(FMPid);
                                progress.data.ProblemUnlockTime.Add(FMPid, now);
                            }

                            //给前端推送Meta解锁信息
                            var unlockMsg = new RedisPushMsg
                            {
                                uid = uid,
                                gid = gid,
                                title = "消息",
                                content = $"纪念品收集已经足够了，该回去了...？",
                                type = "info",
                                show_type = 0
                            };
                            await RedisPublish.Publish(unlockMsg);
                        }
                    }

                    //9. 判断当前区域小题是否达到Meta解锁条件。
                    if (puzzleItem.pgid <= 5)
                    {
                        //当前区域完成的小题数量
                        var currentGroupPuzzleList = new HashSet<int>(puzzleList.Where(x => x.pgid == puzzleItem.pgid && x.answer_type == 0).Select(x => x.pid));
                        var finishedCountInCurrentGroup = progress.data.FinishedProblems.Count(x => currentGroupPuzzleList.Contains(x));

                        var currentGroupMeta = puzzleList.FirstOrDefault(x => x.pgid == puzzleItem.pgid && x.answer_type == 1);
                        if (currentGroupMeta == null)
                        {
                            return (0, 0, 0, "当前区域Meta未设置，请联系管理员！", null);
                        }

                        if (!progress.data.UnlockedProblems.Contains(currentGroupMeta.pid))
                        {

                            var unlockGroupMetaLessCount = RedisNumberCenter.GetUnlockMetaInGroup(puzzleItem.pgid);
                            if (finishedCountInCurrentGroup >= unlockGroupMetaLessCount)
                            {
                                //获取分区名称
                                var puzzleGroupDb = DbFactory.Get<PuzzleGroup>();
                                var groupItem = await puzzleGroupDb.SimpleDb.AsQueryable().Where(x => x.pgid == currentGroupMeta.pgid).WithCache().FirstAsync();
                                var groupName = "分区";
                                if (groupItem != null)
                                {
                                    groupName = groupItem.pg_name;
                                }

                                //解锁当前区域Meta
                                progress.data.UnlockedProblems.Add(currentGroupMeta.pid);
                                progress.data.ProblemUnlockTime[currentGroupMeta.pid] = now;

                                //给前端推送Meta解锁信息
                                var unlockMsg = new RedisPushMsg
                                {
                                    uid = uid,
                                    gid = gid,
                                    title = "消息",
                                    content = $"{groupName} 的元谜题 {currentGroupMeta.title} 已解锁！",
                                    type = "info",
                                    show_type = 0
                                };
                                await RedisPublish.Publish(unlockMsg);
                            }
                        }


                        //目前区域是2，3，4时，判断是否可以解锁下一区域
                        if (puzzleItem.pgid >= 2 && puzzleItem.pgid <= 4)
                        {
                            var unlockNextMetaLessCount = RedisNumberCenter.GetUnlockNextGroupCountInGroup(puzzleItem.pgid);
                            if (finishedCountInCurrentGroup >= unlockNextMetaLessCount)
                            {
                                //解锁下一区域
                                var nextPgid = puzzleItem.pgid + 1;
                                if (!progress.data.UnlockedGroups.Contains(nextPgid))
                                {
                                    progress.data.UnlockGroup(nextPgid, puzzleList, now);
                                    //给前端推送区域解锁信息
                                    var unlockMsg = new RedisPushMsg
                                    {
                                        uid = uid,
                                        gid = gid,
                                        title = "消息",
                                        content = $"飞船接收到了一个新的区域坐标...？",
                                        type = "info",
                                        show_type = 0
                                    };
                                    await RedisPublish.Publish(unlockMsg);
                                }
                            }
                        }
                    }

                    //10. 在当前区域解锁一题
                    if (puzzleItem.pgid <= 6)
                    {
                        progress.data.UnlockNextPuzzle(puzzleItem.pgid, puzzleList, now);
                    }

                }

                //回写存档
                if (puzzleItem.answer_type == 3)
                {
                    await progressDb.SimpleDb.AsUpdateable(progress).IgnoreColumns(it => new { it.power_point, it.power_point_update_time }).ExecuteCommandAsync();
                }
                else
                {
                    await progressDb.SimpleDb.AsUpdateable(progress).IgnoreColumns(it => new { it.finish_time, it.power_point, it.power_point_update_time }).ExecuteCommandAsync();
                }

                //发送推送
                await RedisPublish.Publish(pushMsg);
            }

            //如果extraMessage不为空，则把它作为附加提示信息返回给前端
            if (!string.IsNullOrEmpty(extraMessage))
            {
                successMessage += $"同时你获得了一些信息：{extraMessage}";
                if (string.IsNullOrEmpty(answerLog.message))
                {
                    answerLog.message = extraMessage;
                }
                else
                {
                    answerLog.message += $"附加信息：{extraMessage}";
                }
            }


            await answerLogDb.SimpleDb.AsInsertable(answerLog).ExecuteCommandAsync();

            //11. 检查当前题目是否有扩展内容，如果有，必须刷新题目区域
            //判断跳转
            var extendFlag = 0;
            string extendJumpLocation = "";
            if (puzzleItem.answer_type == 3)
            {
                extendFlag = 1;
                extendJumpLocation = "/article/finalend";
            }
            else if (puzzleItem.pid == 12) //CCBC 16 特殊需求
            {
                extendFlag = 1;
                extendJumpLocation = "/article/g1-end";
            }
            else if (extendFlag != 1) //如果已经需要跳转，则不需要刷新
            {
                extendFlag = string.IsNullOrEmpty(puzzleItem.extend_content) ? 0 : 16; //如果存在扩展，extend_flag应为16，此时前端需要刷新
            }

            //返回回答正确
            return (1, 1, extendFlag, successMessage, extendJumpLocation);
        }
    }
}
