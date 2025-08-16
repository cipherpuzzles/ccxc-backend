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

namespace ccxc_backend.Controllers.Invites
{
    [Export(typeof(HttpController))]
    public class InviteController : HttpController
    {
        [HttpHandler("POST", "/send-invite")]
        public async Task SendInvite(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.TeamLeader);
            if (userSession == null) return;

            var requestJson = request.Json<SendInviteRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
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

            //判断该用户是否已开赛
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress != null)
            {
                await response.BadRequest("开赛后无法再修改队伍信息。");
                return;
            }

            //取得该GID已绑定人数
            var numberOfGroup = await groupBindDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).CountAsync();

            if(numberOfGroup >= Config.SystemConfigLoader.Config.MaxGroupSize)
            {
                await response.BadRequest("组队人数已满，不能发出新邀请。");
                return;
            }

            //取得目标用户信息
            var userDb = DbFactory.Get<User>();

            var sendToUser = await userDb.SimpleDb.AsQueryable().Where(it => it.username == requestJson.username).FirstAsync();
            if(sendToUser == null)
            {
                await response.BadRequest("目标用户不存在或不是未报名用户。");
                return;
            }

            //判断目标用户是否为未报名用户
            if(sendToUser.roleid != 1)
            {
                await response.BadRequest("目标用户不存在或不是未报名用户。");
                return;
            }

            //判断用户是否已被邀请
            var inviteDb = DbFactory.Get<Invite>();
            var vlist = await inviteDb.SimpleDb.AsQueryable()
                .Where(it => it.from_gid == gid && it.to_uid == sendToUser.uid && it.valid == 1)
                .ToListAsync();
            if(vlist != null && vlist.Count > 0)
            {
                await response.BadRequest("该用户已发送过邀请。");
                return;
            }


            //插入邀请信息表
            var newInvite = new invite
            {
                create_time = DateTime.Now,
                from_gid = gid,
                to_uid = sendToUser.uid,
                valid = 1
            };
            await inviteDb.SimpleDb.AsInsertable(newInvite).ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/list-sent-invites")]
        public async Task ListSentInvites(Request request, Response response)
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

            //读取基础数据
            var userDb = DbFactory.Get<User>();
            var userNameDict = (await userDb.SimpleDb.AsQueryable().Select(x => new { x.uid, x.username}).WithCache().ToListAsync()).ToDictionary(it => it.uid, it => it.username);

            //读取仍然为有效状态的邀请
            var inviteDb = DbFactory.Get<Invite>();
            var result = await inviteDb.SimpleDb.AsQueryable().Where(it => it.from_gid == gid && it.valid == 1).ToListAsync();

            var res = result.Select(it =>
            {
                var r = new ListSentResponse.InviteView(it);
                if (userNameDict.ContainsKey(r.to_uid))
                {
                    r.to_username = userNameDict[r.to_uid];
                }
                return r;
            }).ToList();

            await response.JsonResponse(200, new ListSentResponse
            {
                status = 1,
                result = res
            });
        }

        [HttpHandler("POST", "/invalidate-invite")]
        public async Task InvalidateInvite(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.TeamLeader);
            if (userSession == null) return;

            var requestJson = request.Json<IidInviteRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
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

            //读取目标iid
            var inviteDb = DbFactory.Get<Invite>();
            var inviteItem = inviteDb.SimpleDb.GetById(requestJson.iid);

            if(inviteItem == null)
            {
                await response.BadRequest("无效邀请");
                return;
            }

            if(inviteItem.from_gid != gid)
            {
                await response.BadRequest("无修改权限");
            }

            //将目标置为无效
            inviteItem.valid = 0;

            await inviteDb.SimpleDb.AsUpdateable(inviteItem).ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/list-my-invite")]
        public async Task ListMyInvite(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Normal);
            if (userSession == null) return;

            //读取基础数据
            var groupDb = DbFactory.Get<UserGroup>();
            var groupNameDict = (await groupDb.SimpleDb.AsQueryable().Select(x => new { x.gid, x.groupname}).WithCache().ToListAsync()).ToDictionary(it => it.gid, it => it.groupname);

            //读取仍然为有效状态的邀请
            var inviteDb = DbFactory.Get<Invite>();
            var result = await inviteDb.SimpleDb.AsQueryable().Where(it => it.to_uid == userSession.uid && it.valid == 1).ToListAsync();

            var res = result.Select(it =>
            {
                var r = new ListSentResponse.InviteView(it);
                if (groupNameDict.ContainsKey(r.from_gid))
                {
                    r.from_groupname = groupNameDict[r.from_gid];
                }
                return r;
            }).ToList();

            await response.JsonResponse(200, new ListSentResponse
            {
                status = 1,
                result = res
            });
        }

        [HttpHandler("POST", "/decline-invite")]
        public async Task DeclineInvite(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Normal);
            if (userSession == null) return;

            var requestJson = request.Json<IidInviteRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //读取目标iid
            var inviteDb = DbFactory.Get<Invite>();
            var inviteItem = inviteDb.SimpleDb.GetById(requestJson.iid);

            if (inviteItem == null)
            {
                await response.BadRequest("无效邀请");
                return;
            }

            if (inviteItem.to_uid != userSession.uid)
            {
                await response.BadRequest("无修改权限");
            }

            //将目标置为无效
            inviteItem.valid = 2;

            await inviteDb.SimpleDb.AsUpdateable(inviteItem).ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/accept-invite")]
        public async Task AcceptInvite(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Normal);
            if (userSession == null) return;

            var requestJson = request.Json<IidInviteRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //读取用户设置
            var userDb = DbFactory.Get<User>();

            var user = await userDb.SimpleDb.AsQueryable().Where(it => it.uid == userSession.uid).FirstAsync();

            if (user == null)
            {
                await response.BadRequest("活见鬼了");
                return;
            }

            //读取目标iid
            var inviteDb = DbFactory.Get<Invite>();
            var inviteItem = inviteDb.SimpleDb.GetById(requestJson.iid);

            if (inviteItem == null || inviteItem.valid != 1)
            {
                await response.BadRequest("无效邀请");
                return;
            }

            if (inviteItem.to_uid != userSession.uid)
            {
                await response.BadRequest("无修改权限");
            }

            //将目标置为无效
            inviteItem.valid = 3;
            await inviteDb.SimpleDb.AsUpdateable(inviteItem).ExecuteCommandAsync();

            //取得该GID已绑定人数
            var groupBindDb = DbFactory.Get<UserGroupBind>();

            var numberOfGroup = await groupBindDb.SimpleDb.AsQueryable().Where(it => it.gid == inviteItem.from_gid).CountAsync();

            if (numberOfGroup >= Config.SystemConfigLoader.Config.MaxGroupSize)
            {
                await response.BadRequest("组队人数已满，无法加入。");
                return;
            }

            //自动拒绝该用户所有未拒绝的邀请
            await inviteDb.Db.Updateable<invite>().SetColumns(it => it.valid == 2)
                .Where(it => it.to_uid == userSession.uid && it.valid == 1).ExecuteCommandAsync();

            //插入组绑定
            var newGroupBindDb = new user_group_bind
            {
                uid = user.uid,
                gid = inviteItem.from_gid,
                is_leader = 0
            };
            await groupBindDb.SimpleDb.AsInsertable(newGroupBindDb).RemoveDataCache().ExecuteCommandAsync();

            //修改用户设置
            user.roleid = 2;
            user.update_time = DateTime.Now;

            await userDb.SimpleDb.AsUpdateable(user).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }
    }
}
