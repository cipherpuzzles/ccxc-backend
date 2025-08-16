using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using ccxc_backend.Functions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Game
{
    public static class GameProgressExtend
    {
        //建议修改此处所有代码
        public static async Task<SaveData> NewSaveData(DateTime now)
        {
            var data = new SaveData();
            var puzzleDb = DbFactory.Get<Puzzle>();
            //初始解锁的分区数量
            var initGroupCount = RedisNumberCenter.InitialGroupCount;
            for (var i = 1; i <= initGroupCount; i++)
            {
                data.UnlockedGroups.Add(i);

                //取出该区解锁的题目数量
                var unlockPuzzleCount = RedisNumberCenter.GetFirstUnlockInGroup(i);
                //在小题中按照PID顺序，取出对应解锁数量的小题
                var unlockPuzzles = await puzzleDb.SimpleDb.AsQueryable()
                    .Where(x => x.pgid == i && x.answer_type == 0).OrderBy(x => x.pid, SqlSugar.OrderByType.Asc).Select(x => x.pid).WithCache().ToListAsync();
                var unlockPuzzlePids = unlockPuzzles.Take(unlockPuzzleCount).ToList();
                data.UnlockedProblems.UnionWith(unlockPuzzlePids);
                foreach (var pid in unlockPuzzlePids)
                {
                    data.ProblemUnlockTime[pid] = now;
                }
            }

            return data;
        }

        //返回FinalMeta的PID
        public static int GetFMPid => 55;

        public static bool IsFinishedFinalMeta(this SaveData data)
        {
            return data.FinishedProblems.Contains(GetFMPid);
        }

        public static bool IsFMOpen(this SaveData data)
        {
            return data.UnlockedGroups.Contains(6); //终章分区解锁同时FM开放
        }

        public static List<string> GetOpenPuzzleArticleId(this SaveData data)
        {
            var result = new List<string>
            {
                "g1-prologue"
            };
            if (data.FinishedGroups.Contains(1))
            {
                result.Add("g1-end");
                result.Add("main-open");
            }
            for (var gid = 2; gid <= 5; gid++)
            {
                if (data.UnlockedGroups.Contains(gid))
                {
                    result.Add($"g{gid}-prologue");
                }
                if (data.FinishedGroups.Contains(gid))
                {
                    result.Add($"g{gid}-end");
                }
            }
            if (data.IsFMOpen())
            {
                result.Add("g6-prologue");
            }

            return result;
        }
        
        public static bool CanReadPuzzleArticle(this SaveData data, string articleKey)
        {
            if (articleKey == "main-open")
            {
                //正篇开场剧情，需要1区结束
                return data.FinishedGroups.Contains(1);
            }
            else if (articleKey == "finalend")
            {
                //终章结局剧情
                return data.IsFinishedFinalMeta();
            }
            else
            {
                //key形如 g1-prologue g2-end ，解析此key，提取出数字和减号后面的内容。数字表示pgid。
                var keyParts = articleKey.Split('-');
                if (keyParts.Length != 2)
                {
                    return false;
                }
                if (!int.TryParse(keyParts[0].AsSpan(1), out var pgid))
                {
                    return false;
                }

                //如果是开头剧情，判断该分区是否解锁
                if (keyParts[1] == "prologue")
                {
                    return data.UnlockedGroups.Contains(pgid);
                }

                //如果是结尾剧情，判断该分区是否结束
                if (keyParts[1] == "end")
                {
                    return data.FinishedGroups.Contains(pgid);
                }
            }

            return false;
        }

        public static bool CanReadPuzzleGroup(this SaveData data, int pgid)
        {
            return data.UnlockedGroups.Contains(pgid);
        }

        public static void UnlockGroup(this SaveData data, int pgid, List<puzzle> puzzleList, DateTime now)
        {
            //如果已经解锁了，直接返回。
            if (data.UnlockedGroups.Contains(pgid))
            {
                return;
            }

            //解锁分区
            data.UnlockedGroups.Add(pgid);

            //找出该分区需要解锁小题数量
            var unlockPuzzleCount = RedisNumberCenter.GetFirstUnlockInGroup(pgid);

            //找出该分区需要解锁的小题
            var unlockPuzzles = puzzleList.Where(x => x.pgid == pgid && x.answer_type == 0)
                .OrderBy(x => x.pid).Select(x => x.pid).Take(unlockPuzzleCount).ToList();

            //解锁小题
            foreach (var upid in unlockPuzzles)
            {
                UnlockSinglePuzzle(data, now, upid);
            }
        }

        public static void UnlockNextPuzzle(this SaveData data, int pgid, List<puzzle> puzzleList, DateTime now)
        {
            //找出该分区需要解锁的小题
            var unlockPuzzles = puzzleList.Where(x => x.pgid == pgid && x.answer_type == 0)
                .OrderBy(x => x.pid).Select(x => x.pid).ToList();

            //逐一判断此题是否已解锁，找到第一个未解锁的小题解锁
            foreach (var upid in unlockPuzzles)
            {
                if (!data.UnlockedProblems.Contains(upid))
                {
                    UnlockSinglePuzzle(data, now, upid);
                    break;
                }
            }
        }

        private static void UnlockSinglePuzzle(SaveData data, DateTime now, int upid)
        {
            data.UnlockedProblems.Add(upid);
            if (!data.ProblemUnlockTime.ContainsKey(upid))
            {
                data.ProblemUnlockTime.Add(upid, now);
            }
        }
    }
}
