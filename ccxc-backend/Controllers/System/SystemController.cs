using Ccxc.Core.HttpServer;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text;
using System.Threading.Tasks;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using System.Linq;
using Ccxc.Core.Utils;
using SqlSugar;
using ccxc_backend.Functions;
using ccxc_backend.Controllers.Game;
using System.Security.Cryptography;
using System.IO;
using Newtonsoft.Json;
using Ccxc.Core.Utils.ExtensionFunctions;
using ccxc_backend.Controllers.Users;

namespace ccxc_backend.Controllers.System
{
    [Export(typeof(HttpController))]
    public class SystemController : HttpController
    {
        [HttpHandler("POST", "/get-default-setting")]
        public async Task GetDefaultSetting(Request request, Response response)
        {
            IDictionary<string, object> headers = request.Header;

            var isLogin = false;
            if (headers.ContainsKey("user-token"))
            {
                var token = headers["user-token"].ToString();
                if (!string.IsNullOrEmpty(token))
                {
                    isLogin = true;
                }
            }

            if (!isLogin)
            {
                if (Config.SystemConfigLoader.Config.EnableGuestMode == 1)
                {
                    //游客模式
                    await response.JsonResponse(200, new DefaultSettingResponse
                    {
                        project_name = Config.SystemConfigLoader.Config.ProjectName,
                        status = 1,
                        start_time = Config.SystemConfigLoader.Config.StartTime,
                        start_type = 1,
                        guest_mode = 1,
                    });
                    return;
                }
                else
                {
                    await response.JsonResponse(200, new DefaultSettingResponse
                    {
                        project_name = Config.SystemConfigLoader.Config.ProjectName,
                        status = 1,
                        start_time = Config.SystemConfigLoader.Config.StartTime,
                        start_type = 0,
                        guest_mode = 0,
                    });
                    return;
                }
            }

            //已登录
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Normal);
            if (userSession == null) return;

            //尝试读取此用户有无进度
            var startType = 0;
            
            //取得该用户GID
            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == userSession.uid).FirstAsync();
            if (groupBindItem != null)
            {
                var gid = groupBindItem.gid;
                //取得进度
                var progressDb = DbFactory.Get<Progress>();
                var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
                if (progress != null)
                {
                    startType = 1;
                }
            }

            await response.JsonResponse(200, new DefaultSettingResponse
            {
                project_name = Config.SystemConfigLoader.Config.ProjectName,
                status = 1,
                start_time = Config.SystemConfigLoader.Config.StartTime,
                start_type = startType,
                guest_mode = 0,
            });
        }

        [HttpHandler("POST", "/heartbeat")]
        public async Task HeartBeat(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Normal);
            if (userSession == null) return;

            await response.JsonResponse(200, new
            {
                status = 1,
            });
        }

        [HttpHandler("POST", "/heartbeat-puzzle")]
        public async Task HeartBeatPuzzle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Normal, true);
            if (userSession == null) return;

            //取得该用户GID
            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == userSession.uid).FirstAsync();
            if (groupBindItem == null)
            {
                await response.JsonResponse(200, new
                {
                    status = 1,
                    unread = 0,
                    new_message = 0
                });
                return;
            }

            var gid = groupBindItem.gid;

            //取得进度
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();

            //用户已读公告Key
            var cache = DbFactory.GetCache();
            var userReadKey = cache.GetCacheKey($"read_anno_id_for_{userSession.uid}");

            var userRead = await cache.Get<HashSet<int>>(userReadKey);
            if (userRead == null)
            {
                userRead = new HashSet<int>();
            }

            //获取所有公告ID缓存
            var annoIdCacheKey = "ccxc:datacache:announcement_id_cache";
            var annoIdCache = await cache.Get<Dictionary<int, HashSet<int>>>(annoIdCacheKey);

            var allAnnoId = new HashSet<int>();
            if (annoIdCache != null)
            {
                if (annoIdCache.ContainsKey(0))
                {
                    allAnnoId = annoIdCache[0];
                }
            }

            //计算未读公告数目
            var unread = (allAnnoId.Where(annoId => !userRead.Contains(annoId))).Count();

            //计算新消息数目
            var newMessage = 0; //新消息数目
            var messageDb = DbFactory.Get<Message>();
            newMessage = await messageDb.SimpleDb.AsQueryable()
                .Where(it => it.gid == gid && it.direction == 1 && it.is_read == 0).CountAsync();

            await response.JsonResponse(200, new
            {
                status = 1,
                unread,
                new_message = newMessage
            });
        }

        [HttpHandler("POST", "/heartbeat-inner")]
        public async Task HeartBeatInner(Request request, Response response)
        {
            var requestJson = request.Json<HeartbeatInnerRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            if (requestJson.token != "MuksnTyjoS5aZ7y8XIvmW4wy8W0LQ9r4w2ov")
            {
                await response.Forbidden("nnn");
            }

            //计算新消息数目
            var newMessage = 0; //新消息数目
            var messageDb = DbFactory.Get<Message>();
            newMessage = await messageDb.SimpleDb.AsQueryable()
                .Where(it => it.gid == requestJson.gid && it.direction == 1 && it.is_read == 0).CountAsync();

            await response.JsonResponse(200, new
            {
                status = 1,
                nm = newMessage
            });
        }

        [HttpHandler("GET", "/get-sso-prefix")]
        public async Task GetSsoPrefix(Request request, Response response)
        {
            await response.JsonResponse(200, new
            {
                status = 1,
                prefix = Config.SystemConfigLoader.Config.ProjectFrontendPrefix,
                ws_prefix = Config.SystemConfigLoader.Config.WebsocketPrefix
            });
        }


        [HttpHandler("POST", "/get-scoreboard-info")]
        public async Task GetScoreBoardInfo(Request request, Response response)
        {
            if (Config.SystemConfigLoader.Config.UseCachedScoreboard == 1)
            {
                //使用缓存的排行榜数据
                var cachef = DbFactory.GetCache();
                var staticScoreboardKey = cachef.GetCacheKey("scoreboard_static_cache");
                var cacheData = await cachef.Get<ScoreBoardResponse>(staticScoreboardKey);
                if (cacheData != null)
                {
                    //缓存有效，直接返回
                    await response.JsonResponse(200, cacheData);
                    return;
                }
            }

            IDictionary<string, object> headers = request.Header;

            var isLogin = false;
            if (headers.ContainsKey("user-token"))
            {
                var token = headers["user-token"].ToString();
                if (!string.IsNullOrEmpty(token))
                {
                    isLogin = true;
                }
            }

            var progressDb = DbFactory.Get<Progress>();
            UserSession userSession = null;
            SaveData currentUserData = null;
            if (isLogin)
            {
                //已登录
                userSession = await CheckAuth.Check(request, response, AuthLevel.Normal);
                if (userSession == null) return;

                //取得该用户GID
                var groupBindDb = DbFactory.Get<UserGroupBind>();
                var groupBindItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == userSession.uid).FirstAsync();

                if (groupBindItem != null)
                {
                    //组队
                    var gid = groupBindItem.gid;

                    //取得进度
                    var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
                    if (progress != null && progress.data != null)
                    {
                        currentUserData = progress.data;
                    }
                }
            }


            //判断是否已开赛
            var now = DateTime.Now;
            var cache = DbFactory.GetCache();

            var getNumber = false;
            var isBetaUser = false;
            if (now < UnixTimestamp.FromTimestamp(Config.SystemConfigLoader.Config.StartTime))
            {
                //未开赛

                //判断当前用户是否登录
                if (isLogin)
                {
                    //已登录
                    if (userSession.is_betaUser == 1)
                    {
                        //是Beta用户
                        getNumber = true;
                        isBetaUser = true;
                    }
                }
            }
            else
            {
                //已开赛
                getNumber = true;
            }


            //取得redis缓存的排行榜数据
            var scoreboardKey = cache.GetCacheKey("scoreboard_cache");
            if (!isBetaUser) //对beta user禁用缓存
            {
                var cacheData = await cache.Get<ScoreBoardResponse>(scoreboardKey);
                if (cacheData != null && cacheData.cache_time > now.AddMinutes(-1))
                {
                    //缓存有效，直接返回
                    await response.JsonResponse(200, cacheData);
                    return;
                }
            }

            //缓存无效，重新加载数据
            var res = await GetScoreBoardData(now, getNumber);


            //存入缓存
            if (!isBetaUser) //对beta user禁用缓存
            {
                await cache.Put(scoreboardKey, res, 65000);
            }

            await response.JsonResponse(200, res);
        }

        public static async Task<ScoreBoardResponse> GetScoreBoardData(DateTime now, bool getNumber)
        {
            
            var groupDb = DbFactory.Get<UserGroup>();
            var groupList = await groupDb.SimpleDb.AsQueryable().Where(x => x.is_hide != 1).WithCache().ToListAsync();

            var groupUsers = await groupDb.Db
                .Queryable<user_group_bind, user>((b, u) => new JoinQueryInfos(JoinType.Left, b.uid == u.uid))
                .Select((b, u) => new { b.gid, b.is_leader, u.username, u.email, u.theme_color })
                .WithCache()
                .ToListAsync();
            var groupUserDict = groupUsers.GroupBy(x => x.gid)
                .ToDictionary(it => it.Key, it => it.Select(x => new ScoreBoardUser
                {
                    is_leader = x.is_leader,
                    username = x.username,
                    avatar_hash = CryptoUtils.GetAvatarHash(x.email),
                    theme_color = x.theme_color,
                }).ToList());

            var progressDb = DbFactory.Get<Progress>();
            var progressList = await progressDb.SimpleDb.AsQueryable().ToListAsync();
            var progressDict = progressList.ToDictionary(it => it.gid, it => it);

            var scoreBoardList = groupList.Select(it =>
            {
                var r = new ScoreBoardItem
                {
                    gid = it.gid,
                    group_name = it.groupname,
                    group_profile = it.profile
                };

                if (getNumber && progressDict.ContainsKey(it.gid))
                {
                    var progress = progressDict[it.gid];
                    var finalFinish = progress.data.FinishedProblems.Contains(GameProgressExtend.GetFMPid) ? 1 : 0;

                    r.is_finish = progress.is_finish;

                    if (r.is_finish == 1)
                    {
                        var finishTime = progress.finish_time;
                        r.total_time = (finishTime - UnixTimestamp.FromTimestamp(Config.SystemConfigLoader.Config.StartTime)).TotalHours;
                    }

                    r.a = progress.data.FinishedGroups.Count + finalFinish;
                    r.b = progress.data.FinishedProblems.Count;
                    r.meta = progress.data.FinishedGroups.Count + finalFinish;

                    r.percent = 0;
                    if (progress.data.FinishedGroups.Contains(1)) r.percent = 75;
                    if (r.a > 1) r.percent = 96;
                    if (r.a >= 4) r.percent = 99;
                }

                if (groupUserDict.ContainsKey(it.gid))
                {
                    r.users = groupUserDict[it.gid];
                }

                return r;
            }).ToList();

            var res = new ScoreBoardResponse
            {
                status = 1,
                cache_time = now,
                finished_groups = scoreBoardList.Where(it => it.is_finish == 1)
                    .OrderBy(it => it.total_time).ThenBy(it => it.gid).ToList(),
                groups = scoreBoardList.Where(it => it.is_finish != 1).OrderByDescending(it => it.a).ThenByDescending(it => it.b)
                    .ThenBy(it => it.gid).ToList()
            };

            return res;
        }
    }
}
