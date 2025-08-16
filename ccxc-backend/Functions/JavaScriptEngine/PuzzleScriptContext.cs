using Ccxc.Core.HttpServer;
using ccxc_backend.Controllers.Game;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using ccxc_backend.Functions.PowerPoint;
using Jurassic;
using Jurassic.Library;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ccxc_backend.Functions.JavaScriptEngine
{
    public class CallMakePuzzleFinishedRequest
    {
        public bool isCalled { get; set; } = false;//是否调用过
        public int gid { get; set; }
        public int pid { get; set; }
        public int uid { get; set; }
        public string username { get; set; }
        public string message { get; set; }
    }

    public class PuzzleScriptContext : ObjectInstance
    {
        public UserSession user { get; set; }
        public Dictionary<string, string> store { get; set; }
        public Dictionary<int, Dictionary<string, string>> problemStatus { get; set; }
        public SaveData saveData { get; set; }
        public bool isStatusChanged { get; set; } = false;
        public string response { get; set; }
        public CallMakePuzzleFinishedRequest callMakePuzzleFinishedRequest { get; set; } = new CallMakePuzzleFinishedRequest();
        public PuzzleScriptContext(ScriptEngine engine, Dictionary<string, string> store, 
            string request, UserSession user, int gid, SaveData saveData) : base(engine)
        {
            this.user = user;
            this.store = store;
            this.saveData = saveData;
            problemStatus = saveData.ProblemStatus;
            this["request"] = request;
            this["uid"] = user.uid;
            this["username"] = user.username;
            this["gid"] = gid;
            PopulateFunctions();
        }

        [JSFunction(Name = "getStatus")]
        public string GetStatus(string key)
        {
            if (store.ContainsKey(key))
            {
                return store[key];
            }
            return null;
        }

        [JSFunction(Name = "setStatus")]
        public void SetStatus(string key, string value)
        {
            store[key] = value;
        }

        [JSFunction(Name = "response")]
        public void SetResponse(string response)
        {
            this.response = response;
        }

        [JSFunction(Name = "getProgress")]
        public string GetProgress(int pid, string key)
        {
            if (problemStatus.ContainsKey(pid))
            {
                if (problemStatus[pid].ContainsKey(key))
                {
                    return problemStatus[pid][key];
                }
            }
            return null;
        }

        [JSFunction(Name = "setProgress")]
        public void SetProgress(int pid, string key, string value)
        {
            if (!problemStatus.ContainsKey(pid))
            {
                problemStatus[pid] = new Dictionary<string, string>();
            }
            problemStatus[pid][key] = value;
            isStatusChanged = true;
        }

        [JSFunction(Name = "getPuzzleData")]
        public string GetPuzzleData(int pid)
        {
            //取得题目
            var puzzleDb = DbFactory.Get<Puzzle>();
            var puzzle = puzzleDb.SimpleDb.AsQueryable().Where(it => it.pid == pid).First();
            if (puzzle == null)
            {
                throw new JavaScriptException(ErrorType.ReferenceError, "指定的题目不存在");
            }

            //从题目中提取<data></data>部分
            var puzzleHtml = puzzle.html;
            var dataRegex = new Regex(@"<data>([\s\S]*?)<\/data>");
            var dataString = dataRegex.Match(puzzleHtml).Groups[1].Value;

            return dataString;
        }

        [JSFunction(Name = "getStorage")]
        public string GetStorage(string key)
        {
            var cache = DbFactory.GetCache();
            var cacheKey = cache.GetDataKey($"puzzle_script_storage:{key}");
            var value = cache.GetString(cacheKey).GetAwaiter().GetResult();
            return value;
        }

        [JSFunction(Name = "setStorage")]
        public void SetStorage(string key, string value)
        {
            var cache = DbFactory.GetCache();
            var cacheKey = cache.GetDataKey($"puzzle_script_storage:{key}");
            cache.PutString(cacheKey, value).GetAwaiter().GetResult();
        }

        [JSFunction(Name = "getGroupName")]
        public string GetGroupName(int gid)
        {
            var groupDb = DbFactory.Get<UserGroup>();
            var group = groupDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).WithCache().First();
            if (group == null)
            {
                throw new JavaScriptException(ErrorType.ReferenceError, "指定的小组不存在");
            }
            return group.groupname;
        }

        [JSFunction(Name = "getRankAndWinner")]
        public ObjectInstance GetRankAndWinner(int gid)
        {
            var progressDb = DbFactory.Get<Progress>();
            var progressList = progressDb.SimpleDb.AsQueryable()
                .Where(x => x.is_finish == 1)
                .OrderBy(x => x.finish_time, OrderByType.Asc)
                .Select(x => x.gid)
                .ToList();

            var rank = 0;
            var champion = 0;

            if (progressList?.Count > 0)
            {
                rank = progressList.FindIndex(it => it == gid) + 1;
                champion = progressList[0];
            }

            var result = Engine.Object.Construct();
            result["rank"] = rank;
            result["champion"] = champion;

            return result;
        }

        [JSFunction(Name = "httpPostForm")]
        public string HttpPostForm(string url, ObjectInstance form, ObjectInstance headers)
        {
            var formDict = new Dictionary<string, string>();
            foreach (var value in form.Properties)
            {
                var stringKey = value.Key.ToString();
                var stringValue = value.Value.ToString();
                formDict[stringKey] = stringValue;
            }

            var headerDict = new Dictionary<string, string>();
            foreach (var value in headers.Properties)
            {
                var stringKey = value.Key.ToString();
                var stringValue = value.Value.ToString();
                headerDict[stringKey] = stringValue;
            }

            try
            {
                var response = HttpRequest.PostForm(url, formDict, headerDict).GetAwaiter().GetResult();
                return response;
            }
            catch (Exception ex)
            {
                throw new JavaScriptException(ErrorType.Error, "在调用PostForm时发生错误", ex);
            }
        }

        [JSFunction(Name = "costCredit")]
        public bool CostCredit(int gid, int cost)
        {
            var progressDb = DbFactory.Get<Progress>();
            var currentPp = PowerPoint.PowerPoint.GetPowerPoint(progressDb, gid).GetAwaiter().GetResult();

            if (currentPp < cost)
            {
                return false;
            }

            PowerPoint.PowerPoint.UpdatePowerPoint(progressDb, gid, -cost).GetAwaiter().GetResult();
            return true;
        }

        [JSFunction(Name = "addAnswerLog")]
        public void AddAnswerLog(int uid, int gid, int pid, string answer, int status, string message)
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
            answerLogDb.SimpleDb.AsInsertable(answerLog).ExecuteCommand();
        }

        [JSFunction(Name = "makePuzzleFinished")]
        public void MakePuzzleFinished(int gid, int pid, string message)
        {
            callMakePuzzleFinishedRequest.isCalled = true;
            callMakePuzzleFinishedRequest.gid = gid;
            callMakePuzzleFinishedRequest.pid = pid;
            callMakePuzzleFinishedRequest.uid = user.uid;
            callMakePuzzleFinishedRequest.username = user.username;
            callMakePuzzleFinishedRequest.message = message;
        }

        [JSFunction(Name = "hasPuzzleFinished")]
        public bool HasPuzzleFinished(int pid)
        {
            return saveData.FinishedProblems.Contains(pid);
        }

        [JSFunction(Name = "getCheckCount")]
        public ObjectInstance GetCheckCount(int pid)
        {
            var result = Engine.Object.Construct();
            result["count"] = saveData.ProblemAnswerSubmissionsCount.TryGetValue(pid, out int countValue)
                ? countValue : 0;
            result["additionalCount"] = saveData.AdditionalProblemAttemptsCount.TryGetValue(pid, out int additionalCountValue)
                ? additionalCountValue : 0;
            return result;
        }
    }
}
