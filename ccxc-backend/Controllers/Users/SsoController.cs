using Ccxc.Core.HttpServer;
using ccxc_backend.Controllers.Admin;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using log4net.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Users
{
    [Export(typeof(HttpController))]
    public class SsoController : HttpController
    {
        [HttpHandler("POST", "/sso-check")]
        public async Task UserLogin(Request request, Response response)
        {
            var requestJson = request.Json<SsoCheckRequest>();
            //判断请求是否有效
            if (!Validation.Valid(requestJson, out var reason))
            {
                await response.BadRequest(reason);
                return;
            }

            if (requestJson.token == "ccxc")
            {
                var url = new Uri(requestJson.callback_url);
                var host = url.Host;

                if (url.Scheme != "https")
                {
                    await response.BadRequest("error");
                    return;
                }

                //判断host域名是否是 cipherpuzzles.com 或者是 ikp.yt
                if (host.EndsWith("cipherpuzzles.com", StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith("ikp.yt", StringComparison.OrdinalIgnoreCase))
                {
                    await response.OK();
                    return;
                }
            }

            await response.BadRequest("error");
        }

        [HttpHandler("POST", "/sso-login")]
        public async Task SsoLogin(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member);
            if (userSession == null) return;

            var requestJson = request.Json<SsoLoginRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out var reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var uid = userSession.uid;
            //找出用户
            var userDb = DbFactory.Get<User>();
            var user = await userDb.SimpleDb.AsQueryable().Where(x => x.uid == uid).FirstAsync();

            if (user == null)
            {
                await response.BadRequest("用户不存在");
                return;
            }

            var loginLogDb = DbFactory.Get<LoginLog>();
            var loginLog = new login_log
            {
                create_time = DateTime.Now,
                email = user.email,
                username = user.username,
                uid = uid,
                status = 6,
                ip = request.ContextItems["RealIp"].ToString(),
                proxy_ip = request.ContextItems["ForwardIp"].ToString(),
                useragent = request.ContextItems["UserAgent"].ToString(),
                userid = requestJson.userid
            };

            await loginLogDb.SimpleDb.AsInsertable(loginLog).ExecuteCommandAsync();

            await response.OK();
        }
    }
}
