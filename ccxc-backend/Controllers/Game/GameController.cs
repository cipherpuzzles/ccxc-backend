using Ccxc.Core.HttpServer;
using Ccxc.Core.Utils;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using ccxc_backend.Functions;
using ccxc_backend.Functions.PowerPoint;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace ccxc_backend.Controllers.Game
{
    [Export(typeof(HttpController))]
    public class GameController : HttpController
    {
        [HttpHandler("POST", "/start")]
        public async Task Start(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Normal, true);
            if (userSession == null) return;

            //尝试取得该用户组队信息
            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == userSession.uid).FirstAsync();

            if (userSession.uid == -1)
            {
                //游客
                await response.JsonResponse(200, new PuzzleStartResponse
                {
                    status = 1,
                    start_prefix = Config.SystemConfigLoader.Config.GamePrefix,
                    is_first = 0
                });
            }

            var isFirst = 0;
            if (groupBindItem == null)
            {
                //该用户无组队

                await response.BadRequest("必须以组队状态参与。");
                return;
            }
            else
            {
                //有组队
                var gid = groupBindItem.gid;

                //取得进度
                var progressDb = DbFactory.Get<Progress>();
                var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
                if (progress == null)
                {
                    //再次验证当前是否在比赛时间内
                    var now = DateTime.Now;
                    var startTime = UnixTimestamp.FromTimestamp(Config.SystemConfigLoader.Config.StartTime);
                    var endTime = UnixTimestamp.FromTimestamp(Config.SystemConfigLoader.Config.EndTime);

                    if (now < startTime || now >= endTime)
                    {
                        //不在比赛时间范围内。需要二次验证当前用户是否有测试资格
                        var userDb = DbFactory.Get<User>();
                        var userItem = await userDb.SimpleDb.AsQueryable().Where(it => it.uid == userSession.uid).FirstAsync();
                        if (userItem.info_key != "beta_user")
                        {
                            await response.BadRequest("不在比赛时间范围内 [#BU-NotValid]");
                            return;
                        }
                    }

                    isFirst = 1;
                    var data = await GameProgressExtend.NewSaveData(now);

                    //初始化
                    progress = new progress
                    {
                        gid = gid,
                        data = data,
                        score = 0,
                        update_time = now,
                        is_finish = 0,
                        power_point = RedisNumberCenter.InitialPowerPoint, //初始能量点
                        power_point_update_time = now,
                    };

                    await progressDb.SimpleDb.AsInsertable(progress).IgnoreColumns(it => new { it.finish_time }).ExecuteCommandAsync();
                }
            }

            await response.JsonResponse(200, new PuzzleStartResponse
            {
                status = 1,
                start_prefix = Config.SystemConfigLoader.Config.GamePrefix,
                is_first = isFirst
            });
        }
        
        [HttpHandler("POST", "/play/get-article")]
        public async Task GetArticle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            var requestJson = request.Json<PuzzleArticleRequest>();

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
            //组队
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

            //判断用户进度是否达到了打开对应key文章的状态
            var key = requestJson.key;

            var allowRead = progressData.CanReadPuzzleArticle(key);

            if (allowRead == false)
            {
                await response.BadRequest("无法找到请求的内容。");
                return;
            }

            var pAdb = DbFactory.Get<PuzzleArticle>();
            var puzzleArticle = await pAdb.SimpleDb.AsQueryable().Where(it => it.key == key).FirstAsync();

            if (puzzleArticle == null)
            {
                await response.BadRequest("无法找到请求的内容。");
                return;
            }

            if (puzzleArticle.content != null)
            {
                puzzleArticle.content = puzzleArticle.content.Replace("@{##u##}", userSession.username)
                    .Replace("@{##ta##}", userSession.third_pron ?? "他");
            }

            //在content中提取<data xxx>xxx</data>的部分
            var dataRegex = new Regex(@"<data>([\s\S]*?)</data>", RegexOptions.IgnoreCase);
            var dataString = dataRegex.Match(puzzleArticle.content).Groups[1].Value;

            object dataObject = null;
            if (!string.IsNullOrEmpty(dataString))
            {
                try
                {
                    dataObject = JsonConvert.DeserializeObject(dataString);
                }
                catch (Exception e)
                {
                    await response.BadRequest("<data>段数据解析失败。");
                    return;
                }
            }

            //在content中删除<data></data>段
            var content = Regex.Replace(puzzleArticle.content, @"<data[^>]*>.*?</data>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            await response.JsonResponse(200, new PuzzleArticleResponse
            {
                status = 1,
                title = puzzleArticle.title,
                content = content,
                data = dataObject
            });
        }

        [HttpHandler("POST", "/play/get-main-info")]
        public async Task GetMainInfo(Request request, Response response)
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
            //组队
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

            //根据用户进度生成导航栏和可见区域

            //导航栏
            var navBar = new List<NavbarItem>
            {
                new NavbarItem
                {
                    title = "序章",
                    path = "/article/g1-prologue",
                },
                new NavbarItem
                {
                    title = "飞船",
                    path = "/spaceship",
                },
            };

            var navBar2 = new List<NavbarItem>
            {
                new NavbarItem
                {
                    title = "返回主站",
                    path = Config.SystemConfigLoader.Config.ProjectFrontendPrefix,
                },
            };


            //区域信息
            var puzzleAreaInfo = new List<PuzzleBasicInfo>();
            //读取所有区域
            var puzzleGroupDb = DbFactory.Get<PuzzleGroup>();
            var puzzleGroupList = await puzzleGroupDb.SimpleDb.AsQueryable().WithCache().ToListAsync();
            var puzzleGroupDict = puzzleGroupList.ToDictionary(it => it.pgid, it => it);

            var visiblePuzzleGroup = new HashSet<int>(progressData.UnlockedGroups);
            
            //如果默认展示所有的分区，仅有解锁与非解锁区别，则取消注释下面一行。
            //visiblePuzzleGroup.UnionWith(puzzleGroupList.Where(x => x.is_hide == 0).Select(x => x.pgid));

            foreach (var visiblePgid in visiblePuzzleGroup)
            {
                if (puzzleGroupDict.ContainsKey(visiblePgid))
                {
                    var pg = puzzleGroupDict[visiblePgid];
                    puzzleAreaInfo.Add(new PuzzleBasicInfo
                    {
                        pgid = pg.pgid,
                        title = pg.pg_name,
                        is_unlocked = progressData.UnlockedGroups.Contains(pg.pgid) ? 1 : 0,
                        is_finished = progressData.FinishedGroups.Contains(pg.pgid) ? 1 : 0,
                        difficulty = pg.difficulty,
                        stage = pg.pgid == 1 ? 0 : (pg.pgid == 6 ? 1 : 1), //序章-0，本章-1，终章-2
                        content = pg.pg_desc
                    });
                }
            }

            var stage = 0;
            var powerpointName = "信用点";

            //新手区已通过
            if (progressData.FinishedGroups.Contains(1))
            {
                stage = 1;
                navBar.Add(new NavbarItem
                {
                    title = "晶冠星球",
                    path = "/main"
                });
            }

            //Final已解锁
            if (progressData.IsFMOpen())
            {
                stage = 2;
            }

            //Final已完成
            if (progressData.IsFinishedFinalMeta())
            {
                navBar.Add(new NavbarItem
                {
                    title = "最终结局",
                    path = "/article/finalend"
                });
            }

            //取得已解锁的剧情数据
            var libraryIds = progressData.GetOpenPuzzleArticleId();

            //组装返回数据
            await response.JsonResponse(200, new GetMainInfoResponse
            {
                status = 1,
                powerpoint_name = powerpointName,
                stage = stage,
                nav_items = navBar,
                nav_items2 = navBar2,
                puzzle_basic_info = puzzleAreaInfo,
                library_ids = libraryIds,
                show_analysis = Config.SystemConfigLoader.Config.ShowAnalysis
            });

        }

        [HttpHandler("POST", "/play/get-puzzle-info")]
        public async Task GetPuzzleInfo(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            var requestJson = request.Json<PuzzleGroupRequest>();

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
            //组队
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

            //判断用户进度是否达到了打开对应分区的状态
            var pgid = requestJson.pgid;

            var allowRead = progressData.CanReadPuzzleGroup(pgid);

            if (allowRead == false)
            {
                await response.BadRequest("无法找到请求的内容。");
                return;
            }

            //取得分区内容
            var puzzleGroupDb = DbFactory.Get<PuzzleGroup>();
            var puzzleGroup = await puzzleGroupDb.SimpleDb.AsQueryable().Where(x => x.pgid == pgid).WithCache().FirstAsync();
            if (puzzleGroup == null)
            {
                await response.BadRequest("无法找到请求的内容。");
                return;
            }

            //取得分区题目列表
            var puzzleDb = DbFactory.Get<Puzzle>();
            var puzzleBasicList = await puzzleDb.SimpleDb.AsQueryable()
                .Where(x => x.pgid == pgid)
                .Select(x => new { x.pid, x.title, x.answer, x.answer_type, x.extend_data }).WithCache().ToListAsync();

            if (puzzleBasicList?.Count <= 0)
            {
                await response.BadRequest("无法找到请求的内容。");
                return;
            }


            string GetShowedAnswer(int pid, string answer, SaveData progressData)
            {
                if (!progressData.FinishedProblems.Contains(pid))
                {
                    return null;
                }

                if (progressData.ProblemStatus.ContainsKey(pid))
                {
                    if (progressData.ProblemStatus[pid].ContainsKey("__$$ShowAnswer"))
                    {
                        return progressData.ProblemStatus[pid]["__$$ShowAnswer"];
                    }
                }
                return answer;
            }



            //将分区已解锁的题目插入题目列表
            var puzzleList = new List<DetailBasicInfo>();
            foreach (var puzzle in puzzleBasicList)
            {
                var isUnlocked = progressData.UnlockedProblems.Contains(puzzle.pid);

                if (isUnlocked)
                {
                    puzzleList.Add(new DetailBasicInfo
                    {
                        pid = puzzle.pid,
                        title = puzzle.title,
                        puzzle_type = puzzle.answer_type, //0-小题 1-meta 2-mm 3-fm
                        is_finished = progressData.FinishedProblems.Contains(puzzle.pid) ? 1 : 0,
                        answer = GetShowedAnswer(puzzle.pid, puzzle.answer, progressData),
                        is_unlocked = isUnlocked ? 1 : 0,
                        extend_data = puzzle.extend_data
                    });
                }
            }

            //替换当前用户名的占位符
            if (puzzleGroup.pg_desc != null)
            {
                puzzleGroup.pg_desc = puzzleGroup.pg_desc.Replace("@{##u##}", userSession.username).Replace("@{##ta##}", userSession.third_pron ?? "他");
            }


            await response.JsonResponse(200, new GetPuzzleInfoResponse
            {
                status = 1,
                title = puzzleGroup.pg_name,
                content = puzzleGroup.pg_desc,
                detail_basic_info = puzzleList,
            });
        }

        [HttpHandler("POST", "/play/get-detail")]
        public async Task GetDetail(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            var requestJson = request.Json<GetDetailRequest>();

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
            //组队
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
            

            //取得题目内容
            var puzzleDb = DbFactory.Get<Puzzle>();
            var puzzleItem = await puzzleDb.SimpleDb.AsQueryable().Where(x => x.pid == pid).WithCache().FirstAsync();
            if (puzzleItem == null)
            {
                await response.BadRequest("无法找到请求的内容。");
                return;
            }

            //取得分组内容
            var puzzleGroupDb = DbFactory.Get<PuzzleGroup>();
            var puzzleGroup = await puzzleGroupDb.SimpleDb.AsQueryable().Where(x => x.pgid == puzzleItem.pgid).WithCache().FirstAsync();
            if (puzzleGroup == null)
            {
                await response.BadRequest("无法找到请求的内容。(G)");
                return;
            }

            //取得附加答案内容
            var aadb = DbFactory.Get<AdditionalAnswer>();
            var addbStatus = await aadb.SimpleDb.AsQueryable().Where(it => it.pid == pid && it.not_count == 0).WithCache().ToListAsync();
            var adda = 0;
            if (addbStatus?.Count > 0)
            {
                adda = 1;
            }

            var isFinished = progressData.FinishedProblems.Contains(pid);
            var attemptsCount = progressData.ProblemAnswerSubmissionsCount.ContainsKey(pid) ? progressData.ProblemAnswerSubmissionsCount[pid] : 0;
            var attemptsTotal = puzzleItem.attempts_count + (progressData.AdditionalProblemAttemptsCount.ContainsKey(pid) ? progressData.AdditionalProblemAttemptsCount[pid] : 0);

            //组装返回值
            var puzzleView = new PuzzleView(puzzleItem)
            {
                extend_content = isFinished ? puzzleItem.extend_content : null,
                adda = adda,
                pg_name = puzzleGroup.pg_name
            };

            if (puzzleView.content != null)
            {
                puzzleView.content = puzzleView.content.Replace("@{##u##}", userSession.username).Replace("@{##ta##}", userSession.third_pron ?? "他");
            }
            if (puzzleView.extend_content != null)
            {
                puzzleView.extend_content = puzzleView.extend_content.Replace("@{##u##}", userSession.username).Replace("@{##ta##}", userSession.third_pron ?? "他");
            }
            //从html中提取<data xxx>xxxx</data>并过滤掉这部分
            if (puzzleItem.type == 2 && puzzleView.html != null)
            {
                puzzleView.html = Regex.Replace(puzzleView.html, @"<data[^>]*>.*?</data>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }

            var res = new GetPuzzleDetailResponse
            {
                status = 1,
                puzzle = puzzleView,
                is_finish = isFinished ? 1 : 0,
                attempts_count = attemptsCount,
                attempts_total = attemptsTotal,
                power_point = progress.power_point,
                power_point_calc_time = progress.power_point_update_time,
                power_point_increase_rate = RedisNumberCenter.PowerIncreaseRate,
            };

            await response.JsonResponse(200, res);
        }
    }
     
}
