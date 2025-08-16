using Ccxc.Core.HttpServer;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;

namespace ccxc_backend.Controllers.Admin
{
    [Export(typeof(HttpController))]
    public class UserAdminController : HttpController
    {
        [HttpHandler("POST", "/admin/get-user")]
        public async Task GetUser(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<AdminUserQuery>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out var reason))
            {
                await response.BadRequest(reason);
                return;
            }

            if (requestJson.page_num == 0) requestJson.page_num = 1;
            if (requestJson.page_size == 0) requestJson.page_size = 20;

            var cache = DbFactory.GetCache();
            var now = DateTime.Now;
            //登录成功
            var keyPattern = cache.GetUserSessionKey("*");
            var sessions = cache.FindKeys(keyPattern);
            var lastActionDict = (await Task.WhenAll(sessions.Select(async it => await cache.Get<UserSession>(it))))
                .Where(it => it != null && it.is_active == 1)
                .GroupBy(it => it.uid)
                .ToDictionary(it => it.Key, it => it.OrderByDescending(s => s.last_update).FirstOrDefault()?.last_update ?? DateTime.MinValue);

            var userDb = DbFactory.Get<User>();
            IEnumerable<user> allUserList = await userDb.SimpleDb.AsQueryable().WithCache().ToListAsync();

            if (requestJson.is_online == 1)
            {
                allUserList = allUserList.Where(it => lastActionDict.ContainsKey(it.uid) && (now - lastActionDict[it.uid]).TotalMinutes < 5 );
            }

            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindList = await groupBindDb.SimpleDb.AsQueryable().WithCache().ToListAsync();
            var groupBindDict = groupBindList.ToDictionary(it => it.uid, it => it.gid);

            var groupDb = DbFactory.Get<UserGroup>();
            var groupList = await groupDb.SimpleDb.AsQueryable().WithCache().ToListAsync();
            var groupNameDict = groupList.ToDictionary(it => it.gid, it => it.groupname);

            IEnumerable<UserView> userData = allUserList.Select(it =>
            {
                var ret = new UserView(it);
                if (lastActionDict.ContainsKey(it.uid))
                {
                    ret.last_action_time = lastActionDict[it.uid];
                }

                ret.is_beta_user = (it.info_key == "beta_user") ? 1 : 0;

                ret.gid = groupBindDict.ContainsKey(it.uid) ? groupBindDict[it.uid] : 0;
                ret.groupname = groupNameDict.ContainsKey(ret.gid) ? groupNameDict[ret.gid] : null;

                return ret;
            }).OrderBy(it => it.uid);

            if (!string.IsNullOrEmpty(requestJson.username))
            {
                userData = userData.Where(it => it.username.Contains(requestJson.username, StringComparison.InvariantCultureIgnoreCase));
            }

            if (!string.IsNullOrEmpty(requestJson.email))
            {
                userData = userData.Where(it => it.email.Contains(requestJson.email, StringComparison.InvariantCultureIgnoreCase));
            }

            if (requestJson.is_beta_user == 1)
            {
                userData = userData.Where(it => it.is_beta_user == 1);
            }

            var sumRows = userData.Count();
            userData = userData.Skip((requestJson.page_num - 1) * requestJson.page_size).Take(requestJson.page_size);

            await response.JsonResponse(200, new GetAllUserResponse
            {
                status = 1,
                users = userData.ToList(),
                sum_rows = sumRows
            });
        }

        [HttpHandler("POST", "/admin/set-beta-user")]
        public async Task SetBetaUser(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<AdminUidRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out var reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var userDb = DbFactory.Get<User>();

            //修改用户
            var user = new user
            {
                uid = requestJson.uid,
                info_key = "beta_user"
            };

            await userDb.SimpleDb.AsUpdateable(user).UpdateColumns(x => new { x.info_key }).RemoveDataCache().ExecuteCommandAsync();
            await response.OK();
        }

        [HttpHandler("POST", "/admin/remove-beta-user")]
        public async Task RemoveBetaUser(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<AdminUidRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out var reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var userDb = DbFactory.Get<User>();

            //修改用户
            var user = new user
            {
                uid = requestJson.uid,
                info_key = ""
            };

            //查询该用户已登录Session并置为无效
            var cache = DbFactory.GetCache();
            var keyPattern = cache.GetUserSessionKey("*");
            var sessions = cache.FindKeys(keyPattern);

            foreach (var session in sessions)
            {
                var oldSession = await cache.Get<UserSession>(session);
                if (oldSession == null || oldSession.uid != user.uid) continue;
                oldSession.roleid = 0;
                oldSession.is_active = 0;
                oldSession.last_update = DateTime.Now;
                oldSession.inactive_message = $"您的帐号已于 {DateTime.Now:yyyy-MM-dd HH:mm:ss} 被解除内测用户权限，请重新登录。";

                await cache.Put(session, oldSession, Config.SystemConfigLoader.Config.UserSessionTimeout * 1000);
            }

            await userDb.SimpleDb.AsUpdateable(user).UpdateColumns(x => new { x.info_key }).RemoveDataCache().ExecuteCommandAsync();
            await response.OK();
        }

        [HttpHandler("POST", "/admin/set-ban-user")]
        public async Task SetBanUser(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<AdminUidRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out var reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var userDb = DbFactory.Get<User>();
            var userItem = await userDb.SimpleDb.AsQueryable().Where(it => it.uid == requestJson.uid).FirstAsync();

            if (userItem == null)
            {
                await response.BadRequest("请求的UID不存在");
                return;
            }

            //修改用户
            userItem.roleid = -1;
            userItem.update_time = DateTime.Now;

            //查询该用户已登录Session并置为无效
            var cache = DbFactory.GetCache();
            var keyPattern = cache.GetUserSessionKey("*");
            var sessions = cache.FindKeys(keyPattern);

            foreach (var session in sessions)
            {
                var oldSession = await cache.Get<UserSession>(session);
                if (oldSession == null || oldSession.uid != userItem.uid) continue;
                oldSession.roleid = 0;
                oldSession.is_active = 0;
                oldSession.last_update = DateTime.Now;
                oldSession.inactive_message = $"您的帐号已于 {DateTime.Now:yyyy-MM-dd HH:mm:ss} 被封禁，请与管理员联系。";

                await cache.Put(session, oldSession, Config.SystemConfigLoader.Config.UserSessionTimeout * 1000);
            }

            await userDb.SimpleDb.AsUpdateable(userItem).UpdateColumns(x => new { x.roleid, x.update_time }).RemoveDataCache().ExecuteCommandAsync();
            await response.OK();
        }

        [HttpHandler("POST", "/admin/remove-ban-user")]
        public async Task RemoveBanUser(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<AdminUidRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out var reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var userDb = DbFactory.Get<User>();
            var userItem = await userDb.SimpleDb.AsQueryable().Where(it => it.uid == requestJson.uid).FirstAsync();

            if (userItem == null)
            {
                await response.BadRequest("请求的UID不存在");
                return;
            }

            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var userLeaderItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == requestJson.uid).FirstAsync();

            //修改用户
            if (userLeaderItem != null)
            {
                var isLeader = userLeaderItem.is_leader;
                userItem.roleid = isLeader == 1 ? 3 : 2;
            }
            else
            {
                userItem.roleid = 1;
            }

            userItem.update_time = DateTime.Now;

            await userDb.SimpleDb.AsUpdateable(userItem).UpdateColumns(x => new { x.roleid, x.update_time}).RemoveDataCache().ExecuteCommandAsync();
            await response.OK();
        }
    }
}
