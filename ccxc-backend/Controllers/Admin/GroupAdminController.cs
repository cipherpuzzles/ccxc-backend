using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ccxc.Core.HttpServer;
using ccxc_backend.Controllers.Game;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using ccxc_backend.Functions;

namespace ccxc_backend.Controllers.Admin
{
    [Export(typeof(HttpController))]
    public class GroupAdminController : HttpController
    {
        [HttpHandler("POST", "/admin/list-group-name")]
        public async Task ListGroupName(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var userGroupDb = DbFactory.Get<UserGroup>();
            var groupList = await userGroupDb.SimpleDb.AsQueryable()
                .OrderBy(x => x.gid)
                .Select(x => new UserGroupNameInfo { gid = x.gid, groupname = x.groupname }).WithCache().ToListAsync();

            await response.JsonResponse(200, new UserGroupNameListResponse
            {
                status = 1,
                group_name_list = groupList
            });
        }

        [HttpHandler("POST", "/admin/get-group-overview")]
        public async Task GetGroupOverview(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<GetGroupRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            if (requestJson.page_num == 0) requestJson.page_num = 1;
            if (requestJson.page_size == 0) requestJson.page_size = 20;

            var groupDb = DbFactory.Get<UserGroup>();
            IEnumerable<user_group> AllGroupList = await groupDb.SimpleDb.AsQueryable().WithCache().ToListAsync();
            if (requestJson.gid != 0)
            {
                AllGroupList = AllGroupList.Where(it => it.gid == requestJson.gid);
            }

            if (!string.IsNullOrEmpty(requestJson.groupname))
            {
                AllGroupList = AllGroupList.Where(it => it.groupname.Contains(requestJson.groupname, StringComparison.InvariantCultureIgnoreCase));
            }

            int sumRows = 0;
            IEnumerable<user_group> groupList;
            if (requestJson.order == 0)
            {
                AllGroupList = AllGroupList.OrderBy(it => it.gid);
                sumRows = AllGroupList.Count();
                groupList = AllGroupList.Skip((requestJson.page_num - 1) * requestJson.page_size).Take(requestJson.page_size);
            }
            else
            {
                groupList = AllGroupList;
            }

            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindList = await groupBindDb.SimpleDb.AsQueryable().WithCache().ToListAsync();
            var groupBindCountDict = groupBindList.GroupBy(it => it.gid).ToDictionary(it => it.Key, it => it.Count());

            var progressDb = DbFactory.Get<Progress>();
            var progressList = await progressDb.SimpleDb.AsQueryable().ToListAsync();
            var progressDict = progressList.ToDictionary(it => it.gid, it => it);

            var ppRate = RedisNumberCenter.PowerIncreaseRate;

            var resList = groupList.Select(it =>
            {
                var r = new GetGroupOverview
                {
                    gid = it.gid,
                    groupname = it.groupname,
                    profile = it.profile,
                    create_time = it.create_time,
                    is_hide = it.is_hide,
                };

                if (groupBindCountDict.ContainsKey(it.gid))
                {
                    r.member_count = groupBindCountDict[it.gid];
                }

                if (progressDict.ContainsKey(it.gid))
                {
                    var progress = progressDict[it.gid];
                    r.unlock_p1_count = progress.data.UnlockedGroups.Count;
                    r.unlock_p2_count = progress.data.UnlockedProblems.Count;
                    r.finish_p1_count = progress.data.FinishedGroups.Count();
                    r.finish_p2_count = progress.data.FinishedProblems.Count();
                    r.finish_meta_count = progress.data.FinishedGroups.Count() + (progress.is_finish == 1 ? 1 : 0);
                    r.is_finish = progress.is_finish;
                    r.finish_time = progress.finish_time;

                    r.power_point = progress.power_point + ppRate * (int)Math.Floor((DateTime.Now - progress.power_point_update_time).TotalMinutes);
                }

                return r;
            });

            List<GetGroupOverview> res;
            if (requestJson.order == 0)
            {
                res = resList.OrderBy(it => it.gid).ToList();
            }
            else
            {
                resList = resList.OrderByDescending(it => it.is_finish).ThenBy(it => it.finish_time).ThenByDescending(it => it.finish_meta_count).ThenByDescending(it => it.finish_p2_count)
                    .ThenByDescending(it => it.finish_p1_count).ThenBy(it => it.gid);

                sumRows = resList.Count();
                res = resList.Skip((requestJson.page_num - 1) * requestJson.page_size).Take(requestJson.page_size).ToList();
            }

            await response.JsonResponse(200, new GetGroupOverviewResponse
            {
                status = 1,
                groups = res,
                sum_rows = sumRows
            });
        }

        [HttpHandler("POST", "/admin/get-p-user-list")]
        public async Task GetPuzzleUserList(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var puzzleDb = DbFactory.Get<Puzzle>();
            var pidItems = await puzzleDb.SimpleDb.AsQueryable().Select(it => new PidItem
            {
                pid = it.pid,
                pgid = it.pgid,
                title = it.title
            }).OrderBy(it => it.pgid).OrderBy(it => it.pid).WithCache().ToListAsync();

            await response.JsonResponse(200, new GetUserListResponse
            {
                status = 1,
                pid_item = pidItems
            });
        }

        [HttpHandler("POST", "/admin/get-group-detail")]
        public async Task GetGroupDetail(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<GroupAdminRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindUsers = await groupBindDb.SimpleDb.AsQueryable().Where(it => it.gid == requestJson.gid).Select(it => it.uid).ToListAsync();

            var userDb = DbFactory.Get<User>();
            var userList = (await userDb.SimpleDb.AsQueryable().Where(it => groupBindUsers.Contains(it.uid)).ToListAsync())
                .Select(it => new UserNameInfoItem(it)).ToList();

            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == requestJson.gid).FirstAsync();

            var res = new AdminGroupDetailResponse
            {
                status = 1,
                users = userList,
                progress = progress
            };

            await response.JsonResponse(200, res);
        }
        [HttpHandler("POST", "/admin/add-group-powerpoint")]
        public async Task AddGroupPowerPoint(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<GroupAdminPowerpointRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var progressDb = DbFactory.Get<Progress>();
            await Functions.PowerPoint.PowerPoint.UpdatePowerPoint(progressDb, requestJson.gid, requestJson.power_point);

            await response.OK();
        }
        [HttpHandler("POST", "/admin/update-group-hidestatus")]
        public async Task UpdateGroupHideStatus(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<GroupAdminHideRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var groupDb = DbFactory.Get<UserGroup>();
            var group = await groupDb.SimpleDb.AsQueryable().Where(it => it.gid == requestJson.gid).FirstAsync();
            if (group == null)
            {
                await response.BadRequest("Group not found");
                return;
            }

            group.is_hide = requestJson.is_hide;
            await groupDb.SimpleDb.AsUpdateable(group).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/remove-group-member")]
        public async Task RemoveGroupMember(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;
            
            var requestJson = request.Json<GroupMemberRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var userDb = DbFactory.Get<User>();
            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBind = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.gid == requestJson.gid).ToListAsync();

            //查找出当前操作的用户
            var currentUser = await userDb.SimpleDb.AsQueryable().Where(x => x.uid == userSession.uid).FirstAsync();
            if (currentUser == null)
            {
                await response.BadRequest("用户不存在。");
                return;
            }

            //在groupBind中查找是否有此uid
            var groupBindItem = groupBind.FirstOrDefault(x => x.uid == requestJson.uid);
            if (groupBindItem == null)
            {
                await response.BadRequest("该用户不是该队伍成员。");
                return;
            }

            //判断当前待删除用户是否为组长，如果是组长，则需要选择一个新的组长
            if (groupBindItem.is_leader == 1)
            {
                //选择一个新的组长
                var newLeader = groupBind.FirstOrDefault(x => x.uid != requestJson.uid);
                if (newLeader != null)
                {
                    //将此用户设为新组长
                    newLeader.is_leader = 1;

                    //查找出新组长的用户
                    var newLeaderUser = await userDb.SimpleDb.AsQueryable().Where(x => x.uid == newLeader.uid).FirstAsync();
                    if (newLeaderUser == null)
                    {
                        await response.BadRequest("用户不存在。");
                        return;
                    }

                    //如果新组长的roleid是2（组员），则需要将新组长的roleid设置为3（组长）
                    if (newLeaderUser.roleid == 2)
                    {
                        newLeaderUser.roleid = 3;
                        newLeaderUser.update_time = DateTime.Now;
                        await userDb.SimpleDb.AsUpdateable(newLeaderUser).UpdateColumns(x => new {
                            x.roleid,
                            x.update_time
                        }).RemoveDataCache().ExecuteCommandAsync();
                    }

                    await groupBindDb.SimpleDb.AsUpdateable(newLeader).RemoveDataCache().ExecuteCommandAsync();
                }
            }

            //如果当前用户的roleid是2或3，则需要将当前用户的roleid设置为1（未分组用户）
            if (currentUser.roleid == 2 || currentUser.roleid == 3)
            {
                currentUser.roleid = 1;
                currentUser.update_time = DateTime.Now;
                await userDb.SimpleDb.AsUpdateable(currentUser).UpdateColumns(x => new {
                    x.roleid,
                    x.update_time
                }).RemoveDataCache().ExecuteCommandAsync();
            }

            //删除此用户的组队绑定
            await groupBindDb.SimpleDb.AsDeleteable().Where(x => x.gid == requestJson.gid && x.uid == requestJson.uid).RemoveDataCache().ExecuteCommandAsync();

            //查询该用户已登录Session并置为无效
            var cache = DbFactory.GetCache();
            var keyPattern = cache.GetUserSessionKey("*");
            var sessions = cache.FindKeys(keyPattern);

            foreach (var session in sessions)
            {
                var oldSession = await cache.Get<UserSession>(session);
                if (oldSession == null || oldSession.uid != requestJson.uid) continue;
                oldSession.roleid = 0;
                oldSession.is_active = 0;
                oldSession.last_update = DateTime.Now;
                oldSession.inactive_message = $"您的帐号已于 {DateTime.Now:yyyy-MM-dd HH:mm:ss} 被管理员从队伍中移除，请重新登录。";

                await cache.Put(session, oldSession, Config.SystemConfigLoader.Config.UserSessionTimeout * 1000);
            }

            await response.OK();
        }

        [HttpHandler("POST", "/admin/add-group-member")]
        public async Task AddGroupMember(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<GroupMemberRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var userDb = DbFactory.Get<User>();
            var user = await userDb.SimpleDb.AsQueryable().Where(x => x.uid == requestJson.uid).FirstAsync();
            if (user == null)
            {
                await response.BadRequest("用户不存在。");
                return;
            }

            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var userBind = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == requestJson.uid).FirstAsync();
            if (userBind != null)
            {
                if (userBind.gid == requestJson.gid)
                {
                    await response.BadRequest("该用户已加入队伍。");
                    return;
                }
                else
                {
                    await response.BadRequest("该用户已加入其他队伍。");
                    return;
                }
            }

            //添加用户到队伍
            var groupBind = new user_group_bind
            {
                uid = requestJson.uid,
                gid = requestJson.gid,
                is_leader = requestJson.roleid == 3 ? (byte)1 : (byte)0
            };

            await groupBindDb.SimpleDb.AsInsertable(groupBind).RemoveDataCache().ExecuteCommandAsync();

            //如果当前用户的roleid是0或1，则根据设置将当前用户的roleid设置为2或3
            if (user.roleid == 0 || user.roleid == 1)
            {
                user.roleid = requestJson.roleid;
                user.update_time = DateTime.Now;
                await userDb.SimpleDb.AsUpdateable(user).UpdateColumns(x => new {
                    x.roleid,
                    x.update_time
                }).RemoveDataCache().ExecuteCommandAsync();
            }

            //自动拒绝该用户所有未拒绝的邀请
            var inviteDb = DbFactory.Get<Invite>();
            await inviteDb.Db.Updateable<invite>().SetColumns(it => it.valid == 2)
                .Where(it => it.to_uid == requestJson.uid && it.valid == 1).ExecuteCommandAsync();

            //查询该用户已登录Session并置为无效
            var cache = DbFactory.GetCache();
            var keyPattern = cache.GetUserSessionKey("*");
            var sessions = cache.FindKeys(keyPattern);

            foreach (var session in sessions)
            {
                var oldSession = await cache.Get<UserSession>(session);
                if (oldSession == null || oldSession.uid != requestJson.uid) continue;
                oldSession.roleid = 0;
                oldSession.is_active = 0;
                oldSession.last_update = DateTime.Now;
                oldSession.inactive_message = $"您的帐号已于 {DateTime.Now:yyyy-MM-dd HH:mm:ss} 被管理员加入队伍，请重新登录。";

                await cache.Put(session, oldSession, Config.SystemConfigLoader.Config.UserSessionTimeout * 1000);
            }

            await response.OK();
        }

        [HttpHandler("POST", "/admin/delete-group")]
        public async Task DeleteGroup(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;
            
            var requestJson = request.Json<GroupAdminRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBind = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.gid == requestJson.gid).ToListAsync();
            if (groupBind.Count != 0)
            {
                await response.BadRequest("只能删除空队伍。队伍中仍有成员，无法删除。");
                return;
            }

            var groupDb = DbFactory.Get<UserGroup>();
            await groupDb.SimpleDb.AsDeleteable().Where(x => x.gid == requestJson.gid).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/create-group")]
        public async Task CreateGroup(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<user_group>();

            //判断groupname是否为空
            if (string.IsNullOrEmpty(requestJson.groupname))
            {
                await response.BadRequest("队伍名称不能为空。");
                return;
            }

            var groupDb = DbFactory.Get<UserGroup>();
            var group = await groupDb.SimpleDb.AsQueryable().Where(x => x.groupname == requestJson.groupname).FirstAsync();
            if (group != null)
            {
                await response.BadRequest("队伍名称已存在。");
                return;
            }

            var newGroupItem = new user_group
            {
                groupname = requestJson.groupname,
                profile = requestJson.profile,
                create_time = DateTime.Now,
                update_time = DateTime.Now,
                is_hide = 0
            };

            await groupDb.SimpleDb.AsInsertable(newGroupItem).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/update-group-profile")]
        public async Task UpdateGroupProfile(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<user_group>();

            //判断groupname是否为空
            if (string.IsNullOrEmpty(requestJson.groupname))
            {
                await response.BadRequest("队伍名称不能为空。");
                return;
            }

            var groupDb = DbFactory.Get<UserGroup>();
            var group = await groupDb.SimpleDb.AsQueryable().Where(x => x.gid == requestJson.gid).FirstAsync();
            if (group == null)
            {
                await response.BadRequest("队伍不存在。");
                return;
            }

            //如果用户试图修改队伍名称，则需要查找除当前队伍外，请求的名称是否已存在
            if (group.groupname != requestJson.groupname)
            {
                var dgroup = await groupDb.SimpleDb.AsQueryable().Where(x => x.groupname == requestJson.groupname && x.gid != requestJson.gid).FirstAsync();
                if (dgroup != null)
                {
                    await response.BadRequest("队伍名称已存在。");
                    return;
                }

                group.groupname = requestJson.groupname;
            }

            group.profile = requestJson.profile;
            group.update_time = DateTime.Now;

            await groupDb.SimpleDb.AsUpdateable(group).UpdateColumns(x => new {
                x.groupname,
                x.profile,
                x.update_time
            }).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/get-sim-login-session")]
        public async Task GetSimLoginSession(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<GroupAdminRequest>();

            var randomUid = new Random().Next(100000, 99999999);

            var uuid = Guid.NewGuid().ToString("n");
            var sk = CryptoUtils.GetRandomKey();
            var newSession = new UserSession
            {
                uid = randomUid,
                gid = requestJson.gid,
                username = $"[STAFF]{userSession.username}",
                roleid = 4, // STAFF
                color = Controllers.Users.UserController.GetRandomThemeColor(),
                third_pron = "TA",
                token = uuid,
                sk = sk,
                login_time = DateTime.Now,
                last_update = DateTime.Now,
                is_active = 1,
                inactive_message = "",
                is_betaUser = 1
            };

            var cache = DbFactory.GetCache();

            var sessionKey = cache.GetUserSessionKey(uuid);
            await cache.Put(sessionKey, newSession, 14400000);

            await response.JsonResponse(200, new
            {
                status = 1,
                sim_user = new
                {
                    uid = newSession.uid,
                    username = newSession.username,
                    roleid = newSession.roleid,
                    token = uuid,
                    sk = sk,
                    etc = "52412",
                    color = newSession.color,
                }
            });
        }
    }
}