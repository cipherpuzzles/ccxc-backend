using Ccxc.Core.Utils;
using ccxc_backend.Controllers.Game;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using Jurassic.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Functions.JavaScriptEngine
{
    public static class JSEFunctions
    {
        public static async Task<(int code, int answerStatus, int extendFlag, string message, string location)> MakePuzzleFinished(int gid, int pid, int uid, string username, string message)
        {
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress == null)
            {
                Logger.Error($"[MakePuzzleFinished] Failed. GID:{gid} PID:{pid} UID:{uid} message:{message} 原因：没有进度");
                return (-1, 0, 0, "没有进度，请返回首页重新开始。", null);
            }

            if (progress.data == null)
            {
                Logger.Error($"[MakePuzzleFinished] Failed. GID:{gid} PID:{pid} UID:{uid} message:{message} 原因：没有存档数据");
                return (-1, 0, 0, "未找到可用存档，请联系管理员。", null);
            }

            if (progress.data.FinishedProblems.Contains(pid))
            {
                Logger.Error($"[MakePuzzleFinished] Failed. GID:{gid} PID:{pid} UID:{uid} message:{message} 原因：题目已完成");
                return (-1, 0, 0, "题目已完成。", null);
            }

            var puzzleDb = DbFactory.Get<Puzzle>();
            var puzzleList = await puzzleDb.SimpleDb.AsQueryable().WithCache().ToListAsync();
            var puzzleItem = puzzleList.Where(x => x.pid == pid).FirstOrDefault();
            if (puzzleItem == null)
            {
                Logger.Error($"[MakePuzzleFinished] Failed. GID:{gid} PID:{pid} UID:{uid} message:{message} 原因：题目不存在");
                return (-1, 0, 0, "题目不存在，请联系管理员。", null);
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

            var result = await OperateController.PushNextHelper(progress, progressDb, puzzleItem, puzzleList,
                answerLog, answerLogDb, uid, gid, username, now, message);

            return result;
        }
    }
}
