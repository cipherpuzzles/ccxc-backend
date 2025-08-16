using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ccxc.Core.HttpServer;
using Ccxc.Core.Plugins;
using Ccxc.Core.Plugins.DataModels;
using ccxc_backend.Controllers.Game;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using SqlSugar;

namespace ccxc_backend.Functions.Plugins
{
    public class PluginAPI : IPluginAPI
    {
        public async Task<Ccxc.Core.Plugins.DataModels.UserSession> CheckAuth(Request request, Response response, AuthLevel authLevel, bool onlyInGaming = false)
        {
            var user = await Controllers.CheckAuth.Check(request, response, (Controllers.AuthLevel)authLevel, onlyInGaming);
            var result = new Ccxc.Core.Plugins.DataModels.UserSession
            {
                uid = user.uid,
                username = user.username,
                roleid = user.roleid
            };
            return result;
        }

        public async Task<ProgressData> GetProgressData(int uid)
        {
            //取得该用户GID
            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == uid).FirstAsync();

            if (groupBindItem == null)
            {
                //尝试去redis里取出对应uid的登录session
                var cache = DbFactory.GetCache();
                var keyPattern = cache.GetUserSessionKey("*");
                var sessions = cache.FindKeys(keyPattern);
                var spSession = (await Task.WhenAll(sessions.Select(async it => await cache.Get<DataModels.UserSession>(it))))
                    .Where(it => it != null && it.is_active == 1 && it.uid == uid).FirstOrDefault();

                if (spSession != null)
                {
                    if (spSession.roleid == 4)
                    {
                        groupBindItem = new user_group_bind
                        {
                            uid = uid,
                            gid = spSession.gid,
                            is_leader = 0
                        };
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }

            var gid = groupBindItem.gid;

            //取得进度
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress == null)
            {
                return null;
            }

            var progressData = progress.data;
            if (progressData == null)
            {
                return null;
            }

            //创建一个progressData的deep clone，以免插件中的改动影响到真正progressData
            var resultData = new ProgressData
            {
                gid = gid,
                FinishedProblems = [.. progressData.FinishedProblems.ToList()],
                UnlockedProblems = [.. progressData.UnlockedProblems.ToList()],
                FinishedGroups = [.. progressData.FinishedGroups.ToList()],
                UnlockedGroups = [.. progressData.UnlockedGroups.ToList()],
                ProblemUnlockTime = new Dictionary<int, DateTime>(progressData.ProblemUnlockTime.ToDictionary(x => x.Key, x => x.Value)),
                ProblemAnswerSubmissionsCount = new Dictionary<int, int>(progressData.ProblemAnswerSubmissionsCount.ToDictionary(x => x.Key, x => x.Value)),
                AdditionalProblemAttemptsCount = new Dictionary<int, int>(progressData.AdditionalProblemAttemptsCount.ToDictionary(x => x.Key, x => x.Value)),
                OpenedHints = new Dictionary<int, HashSet<int>>(progressData.OpenedHints.ToDictionary(x => x.Key, x => new HashSet<int>(x.Value))),
                ProblemStatus = new Dictionary<int, Dictionary<string, string>>(progressData.ProblemStatus.ToDictionary(x => x.Key, x => new Dictionary<string, string>(x.Value))),
                IsFinish = progress.is_finish == 1,
                FinishTime = progress.finish_time
            };
            return resultData;
        }

        public async Task SavePuzzleProgress(int gid, int pid, string key, string value)
        {
            //取出progressData
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress == null)
            {
                throw new Exception("[P] 队伍不存在进度。请返回首页重新开始。");
            }

            var progressData = progress.data ?? throw new Exception("[P] 未找到可用存档，请联系管理员。");

            //更新progressData
            if (!progress.data.ProblemStatus.ContainsKey(pid))
            {
                progress.data.ProblemStatus[pid] = [];
            }
            progress.data.ProblemStatus[pid][key] = value;

            //保存进度
            await progressDb.SimpleDb.AsUpdateable(progress).UpdateColumns(x => new { x.data }).ExecuteCommandAsync();
        }


        public async Task<bool> CostCredit(int gid, int cost)
        {
            var progressDb = DbFactory.Get<Progress>();
            var currentPp = await PowerPoint.PowerPoint.GetPowerPoint(progressDb, gid);

            if (currentPp < cost)
            {
                return false;
            }

            await PowerPoint.PowerPoint.UpdatePowerPoint(progressDb, gid, -cost);
            return true;
        }

        public async Task AddAnswerLog(int uid, int gid, int pid, string answer, int status, string message)
        {
            var answerLogDb = DbFactory.Get<AnswerLog>();
            var answerLog = new answer_log
            {
                create_time = DateTime.Now,
                uid = uid,
                gid = gid,
                pid = pid,
                answer = answer,
                status = (byte)status,
                message = message
            };
            await answerLogDb.SimpleDb.AsInsertable(answerLog).ExecuteCommandAsync();
        }

        public async Task<(int code, int answerStatus, int extendFlag, string message, string location)> MakePuzzleFinished(
            int uid, string username, int gid, int pid, string message)
        {
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress == null)
            {
                return (0, 0, 0, "无进度信息", null);
            }

            if (progress.data == null)
            {
                return (0, 0, 0, "存档出错", null);
            }

            if (progress.data.FinishedProblems.Contains(pid))
            {
                return (0, 0, 0, "该题目已完成", null);
            }

            var puzzleDb = DbFactory.Get<Puzzle>();
            var puzzleList = await puzzleDb.SimpleDb.AsQueryable().WithCache().ToListAsync();
            var puzzleItem = puzzleList.Where(x => x.pid == pid).FirstOrDefault();
            if (puzzleItem == null)
            {
                return (0, 0, 0, "题目不存在", null);
            }

            var now = DateTime.Now;
            var answerLogDb = DbFactory.Get<AnswerLog>();
            var answerLog = new answer_log
            {
                create_time = now,
                uid = uid,
                gid = gid,
                pid = pid,
                answer = "",
            };

            var resultTuple = await OperateController.PushNextHelper(progress, progressDb, puzzleItem, puzzleList, 
                answerLog, answerLogDb, uid, gid, username, now, message);

            return resultTuple;
        }
    }
}