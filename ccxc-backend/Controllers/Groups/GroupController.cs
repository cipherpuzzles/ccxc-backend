using Ccxc.Core.HttpServer;
using Ccxc.Core.Utils;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Groups
{
    [Export(typeof(HttpController))]
    public class GroupController : HttpController
    {
        [HttpHandler("POST", "/create-group")]
        public async Task CreateGroup(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Normal);
            if (userSession == null) return;

            var requestJson = request.Json<CreateGroupRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out var reason))
            {
                await response.BadRequest(reason);
                return;
            }
            var now = DateTime.Now;

            //判断当前用户不属于任何组队
            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == userSession.uid).FirstAsync();

            if(groupBindItem != null)
            {
                await response.BadRequest("Emmmmm, 请勿重复新建组队。");
                return;
            }

            //取得用户数据
            var userDb = DbFactory.Get<User>();
            var userItem = await userDb.SimpleDb.AsQueryable().Where(x => x.uid == userSession.uid).FirstAsync();

            if (userItem == null)
            {
                await response.Unauthorized("服务器提出了一个问题：你为何存在？");
                return;
            }

            if (string.IsNullOrWhiteSpace(requestJson.groupname))
            {
                await response.BadRequest("组队名称不能全为空白字符。");
                return;
            }

            if (requestJson.groupname.Length > 32)
            {
                await response.BadRequest("组队名称太长。");
                return;
            }

            if (requestJson.profile.Length > 350)
            {
                await response.BadRequest("队伍简介不能太长。");
                return;
            }

            var groupDb = DbFactory.Get<UserGroup>();
            //检查队名是否重复
            var groupNameCount = await groupDb.SimpleDb.AsQueryable().Where(x => x.groupname == requestJson.groupname).CountAsync();
            if (groupNameCount > 0)
            {
                await response.BadRequest("队名不能重复");
                return;
            }

            //新建组队
            var newGroup = new user_group
            {
                groupname = requestJson.groupname,
                profile = requestJson.profile,
                create_time = now,
                update_time = now
            };

            var newGid = await groupDb.SimpleDb.AsInsertable(newGroup).RemoveDataCache().ExecuteReturnIdentityAsync();

            //自动拒绝该用户所有未拒绝的邀请
            var inviteDb = DbFactory.Get<Invite>();
            await inviteDb.Db.Updateable<invite>().SetColumns(it => it.valid == 2)
                .Where(it => it.to_uid == userSession.uid && it.valid == 1).RemoveDataCache().ExecuteCommandAsync();


            //修改用户组队绑定
            var user = new user
            {
                uid = userSession.uid,
                roleid = 3,
                update_time = now
            };
            await userDb.SimpleDb.AsUpdateable(user).UpdateColumns(x => new { x.roleid, x.update_time }).RemoveDataCache().ExecuteCommandAsync();

            var newGroupBind = new user_group_bind
            {
                uid = userSession.uid,
                gid = newGid,
                is_leader = 1
            };
            await groupBindDb.SimpleDb.AsInsertable(newGroupBind).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/edit-group")]
        public async Task EditGroup(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.TeamLeader);
            if (userSession == null) return;

            var requestJson = request.Json<CreateGroupRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out var reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //取得该用户GID
            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == userSession.uid).FirstAsync();

            if (groupBindItem == null)
            {
                await response.BadRequest("未确定组队？");
                return;
            }

            var gid = groupBindItem.gid;

            if (string.IsNullOrWhiteSpace(requestJson.groupname))
            {
                await response.BadRequest("组队名称不能全为空白字符。");
                return;
            }

            if (requestJson.groupname.Length > 32)
            {
                await response.BadRequest("组队名称太长。");
                return;
            }

            if (requestJson.profile.Length > 350)
            {
                await response.BadRequest("队伍简介不能太长。");
                return;
            }


            //取出组队
            var groupDb = DbFactory.Get<UserGroup>();

            var groupNameCount = await groupDb.SimpleDb.AsQueryable().Where(x => x.gid != gid && x.groupname == requestJson.groupname).CountAsync();

            if (groupNameCount > 0)
            {
                await response.BadRequest("队名不能重复");
                return;
            }

            var groupItem = await groupDb.SimpleDb.AsQueryable().Where(x => x.gid == gid).FirstAsync();
            if (groupItem == null)
            {
                await response.BadRequest("组队出错？？？");
                return;
            }

            //判断当前在允许新建组队的时间范围内，否则队名不能修改
            if (requestJson.groupname != groupItem.groupname)
            {

                //判断该用户是否已开赛
                var progressDb = DbFactory.Get<Progress>();
                var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
                if (progress != null)
                {
                    await response.BadRequest("开赛后无法再修改队伍信息。");
                    return;
                }

                groupItem.groupname = requestJson.groupname;
            }

            //编辑并保存
            groupItem.profile = requestJson.profile;
            groupItem.update_time = DateTime.Now;

            await groupDb.SimpleDb.AsUpdateable(groupItem).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/delete-group")]
        public async Task DeleteGroup(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.TeamLeader);
            if (userSession == null) return;

            //取得该用户GID
            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == userSession.uid).FirstAsync();

            if (groupBindItem == null)
            {
                await response.BadRequest("未确定组队？");
                return;
            }

            var gid = groupBindItem.gid;

            //判断该用户是否已开赛
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress != null)
            {
                await response.BadRequest("开赛后无法再修改队伍信息。");
                return;
            }

            //取出组队
            var groupDb = DbFactory.Get<UserGroup>();

            var groupItem = await groupDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (groupItem == null)
            {
                await response.BadRequest("幽灵组队出现了！！！！！！！！！");
                return;
            }

            //取出组队所有成员并置为无组队状态
            var groupUids = await groupBindDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).Select(it => it.uid).ToListAsync();

            var userDb = DbFactory.Get<User>();

            var updateUser = new List<user>();
            foreach(var uid in groupUids)
            {
                var user = new user
                {
                    uid = uid,
                    roleid = 1,
                    update_time = DateTime.Now
                };
                updateUser.Add(user);
            }
            await userDb.SimpleDb.AsUpdateable(updateUser).UpdateColumns(x => new { x.roleid, x.update_time}).RemoveDataCache().ExecuteCommandAsync();

            //自动撤销本组发出的所有未拒绝的邀请
            var inviteDb = DbFactory.Get<Invite>();
            await inviteDb.Db.Updateable<invite>().SetColumns(it => it.valid == 0)
                .Where(it => it.from_gid == gid && it.valid == 1).RemoveDataCache().ExecuteCommandAsync();

            //删除组队信息
            await groupDb.SimpleDb.AsDeleteable().Where(it => it.gid == gid).RemoveDataCache().ExecuteCommandAsync();

            //删除组队绑定信息
            await groupBindDb.SimpleDb.AsDeleteable().Where(it => it.gid == gid).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/exit-group")]
        public async Task ExitGroup(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member);
            if (userSession == null) return;

            if(userSession.roleid != (int)AuthLevel.Member)
            {
                await response.BadRequest("只有队员才可退出组队");
            }

            //取得该用户GID
            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == userSession.uid).FirstAsync();
            if (groupBindItem == null)
            {
                await response.BadRequest("未确定组队？");
                return;
            }

            var gid = groupBindItem.gid;

            //判断该用户是否已开赛
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress != null)
            {
                await response.BadRequest("开赛后无法再修改队伍信息。");
                return;
            }

            //修改用户权限信息
            var userDb = DbFactory.Get<User>();

            var user = new user
            {
                uid = userSession.uid,
                roleid = 1,
                update_time = DateTime.Now
            };

            await userDb.SimpleDb.AsUpdateable(user).UpdateColumns(x => new { x.roleid, x.update_time }).RemoveDataCache().ExecuteCommandAsync();

            //删除组队绑定
            await groupBindDb.SimpleDb.AsDeleteable().Where(it => it.uid == userSession.uid).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/remove-group-member")]
        public async Task RemoveGroupMember(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.TeamLeader);
            if (userSession == null) return;

            var requestJson = request.Json<RemoveGroupRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out var reason))
            {
                await response.BadRequest(reason);
                return;
            }

            if(requestJson.uid == userSession.uid)
            {
                await response.BadRequest("目标不能是自己");
                return;
            }

            //取得GID
            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == userSession.uid).FirstAsync();
            if (groupBindItem == null)
            {
                await response.BadRequest("未确定组队？");
                return;
            }

            var gid = groupBindItem.gid;

            //判断该用户是否已开赛
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress != null)
            {
                await response.BadRequest("开赛后无法再修改队伍信息。");
                return;
            }

            //取得目标用户的gid

            var targetUserBindItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == requestJson.uid).FirstAsync();
            if (targetUserBindItem == null)
            {
                await response.BadRequest("什么人类？？？？？");
                return;
            }

            var targetGid = targetUserBindItem.gid;

            if(gid != targetGid)
            {
                await response.Unauthorized("权限不足。无法操作其他用户");
                return;
            }

            //修改用户权限信息
            var userDb = DbFactory.Get<User>();

            var user = new user
            {
                uid = requestJson.uid,
                roleid = 1,
                update_time = DateTime.Now
            };

            await userDb.SimpleDb.AsUpdateable(user).UpdateColumns(x => new { x.roleid, x.update_time }).RemoveDataCache().ExecuteCommandAsync();

            //删除组队绑定
            await groupBindDb.SimpleDb.AsDeleteable().Where(it => it.uid == requestJson.uid).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }
    }
}
