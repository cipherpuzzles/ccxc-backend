using Ccxc.Core.HttpServer;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Admin
{
    [Export(typeof(HttpController))]
    public class OrganizerAdminController : HttpController
    {
        [HttpHandler("POST", "/admin/get-organizer-list")]
        public async Task GetOrganizerList(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            //从用户列表中筛选出 roleid >= 4 的用户（组织者和管理员）
            var userDb = DbFactory.Get<User>();
            var organizerList = await userDb.SimpleDb.AsQueryable()
                .Where(x => x.roleid >= 4)
                .ToListAsync();

            var organizerData = organizerList.Select(it => {
                var ret = new UserView(it)
                {
                    is_beta_user = (it.info_key == "beta_user") ? 1 : 0
                };
                return ret;
            });

            await response.JsonResponse(200, new {
                status = 1,
                data = organizerData
            });
        }

        [HttpHandler("POST", "/admin/set-organizer-role")]
        public async Task SetOrganizerRole(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Administrator);
            if (userSession == null) return;

            var requestJson = request.Json<OrganizerAdminRequest>();
            
            if (requestJson.uid == 0)
            {
                await response.BadRequest("未选中用户");
                return;
            }

            if (requestJson.roleid != 1 && requestJson.roleid != 4 && requestJson.roleid != 5)
            {
                //只允许设置为 1(取消), 4(组织者), 5(管理员)
                await response.BadRequest("不可将用户设置为该角色");
                return;
            }

            var userDb = DbFactory.Get<User>();
            var user = await userDb.SimpleDb.AsQueryable().Where(x => x.uid == requestJson.uid).FirstAsync();
            if (user == null)
            {
                await response.BadRequest("用户不存在");
                return;
            }

            var oldRoleId = user.roleid;

            user.roleid = requestJson.roleid;
            user.update_time = DateTime.Now;
            await userDb.SimpleDb.AsUpdateable(user).UpdateColumns(x => new {
                x.roleid,
                x.update_time
            }).RemoveDataCache().ExecuteCommandAsync();

            //将用户从任何可能的组队中移除。
            var groupBindDb = DbFactory.Get<UserGroupBind>();
            await groupBindDb.SimpleDb.AsDeleteable().Where(x => x.uid == requestJson.uid).RemoveDataCache().ExecuteCommandAsync();

            //使当前用户所有登录session失效。
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

                var executeVerb = requestJson.roleid == 0 ? "取消" : "授予";
                var roleName = "";
                if (requestJson.roleid == 0)
                {
                    roleName = oldRoleId == 4 ? "出题组成员" : "管理员";
                }
                else
                {
                    roleName = requestJson.roleid == 4 ? "出题组成员" : "管理员";
                }
                oldSession.inactive_message = $"您的帐号已于 {DateTime.Now:yyyy-MM-dd HH:mm:ss} 被" + 
                    $"{executeVerb} {roleName} 权限，请重新登录。";

                await cache.Put(session, oldSession, Config.SystemConfigLoader.Config.UserSessionTimeout * 1000);
            }

            await response.OK();
        }
    }
}
