using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using Jurassic;
using Jurassic.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ccxc_backend.Functions.JavaScriptEngine
{
    public class CheckAnswerScriptContext : ObjectInstance
    {
        public bool result { get; set; } = false;
        public string extraMessage { get; set; } = null;
        public bool isHitMilestone { get; set; } = false;
        public Dictionary<int, Dictionary<string, string>> problemStatus { get; set; }
        public bool isStatusChanged { get; set; } = false;
        public int pid { get; set; } = 0;

        public CheckAnswerScriptContext(ScriptEngine engine, string originalAnswer, string answer,
            UserSession user, int gid, int pid, Dictionary<int, Dictionary<string, string>> problemStatus) : base(engine)
        {
            this.problemStatus = problemStatus;
            this.pid = pid;
            this["answer"] = answer;
            this["originalAnswer"] = originalAnswer;
            this["uid"] = user.uid;
            this["username"] = user.username;
            this["gid"] = gid;
            this["pid"] = pid;
            PopulateFunctions();
        }

        [JSFunction(Name = "setResult")]
        public void SetResult(bool result)
        {
            this.result = result;
        }

        [JSFunction(Name = "setExtraMessage")]
        public void SetExtraMessage(string message)
        {
            extraMessage = message;
        }

        [JSFunction(Name = "hitMilestone")]
        public void HitMilestone(bool hit)
        {
            isHitMilestone = hit;
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

        [JSFunction(Name = "setShowAnswer")]
        public void SetShowAnswer(string answer)
        {
            if (!problemStatus.ContainsKey(pid))
            {
                problemStatus[pid] = new Dictionary<string, string>();
            }
            problemStatus[pid]["__$$ShowAnswer"] = answer;
            isStatusChanged = true;
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
    }
}
