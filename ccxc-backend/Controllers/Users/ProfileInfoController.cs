using Ccxc.Core.HttpServer;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using ccxc_backend.Functions;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Users
{
    [Export(typeof(HttpController))]
    public class ProfileInfoController : HttpController
    {
        [HttpHandler("POST", "/get-profileInfo")]
        public async Task UserReg(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Normal);
            if (userSession == null) return;

            var userDb = DbFactory.Get<User>();
            var userItem = await userDb.SimpleDb.AsQueryable().Where(x => x.uid == userSession.uid).FirstAsync();

            var res = new MyProfileResponse();
            if (userItem == null)
            {
                await response.Unauthorized("服务器提出了一个问题：你为何存在？");
                return;
            }

            //取出当前用户信息
            res.user_info = new UserInfo(userItem);

            //读取分组信息

            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == userSession.uid).FirstAsync();
            if (groupBindItem != null)
            {
                var gid = groupBindItem.gid;
                var groupDb = DbFactory.Get<UserGroup>();
                var groupItem = await groupDb.SimpleDb.AsQueryable().Where(x => x.gid == gid).FirstAsync();
                if (groupItem != null)
                {
                    res.group_info = new GroupInfo(groupItem)
                    {
                        member_list = new List<UserInfo>()
                    };

                    var memberUids = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.gid == gid).Select(x => x.uid).ToListAsync();
                    if (memberUids?.Count > 0)
                    {
                        var userList = await userDb.SimpleDb.AsQueryable().Where(x => memberUids.Contains(x.uid)).ToListAsync();
                        res.group_info.member_list = userList.Select(it => new UserInfo(it) {
                            email = "",
                            phone = "",
                            avatar_hash = CryptoUtils.GetAvatarHash(it.email)
                        }).ToList();
                    }
                }
            }

            res.status = 1;
            await response.JsonResponse(200, res);
        }
    }
}
