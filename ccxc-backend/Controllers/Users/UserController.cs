using Ccxc.Core.HttpServer;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using ccxc_backend.Functions;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Users
{
    [Export(typeof(HttpController))]
    public class UserController : HttpController
    {
        [HttpHandler("POST", "/user-reg")]
        public async Task UserReg(Request request, Response response)
        {
            var requestJson = request.Json<UserRegRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            if (string.IsNullOrWhiteSpace(requestJson.username))
            {
                await response.BadRequest("用户名不能是空白字符");
                return;
            }

            if (requestJson.username.Length > 25)
            {
                await response.BadRequest("用户名长度过于长了");
                return;
            }

            var cache = DbFactory.GetCache();
            var cKey = cache.GetCacheKey($"captcha_{requestJson.nonce}");
            var code = await cache.Get<string>(cKey);

            if (string.IsNullOrEmpty(code))
            {
                await response.BadRequest("验证码错误");
                return;
            }

            if (code.ToLower() != requestJson.code.ToLower())
            {
                await response.BadRequest("验证码错误");
                return;
            }

            //数据库对象
            var userDb = DbFactory.Get<User>();

            //判断是否重复
            var userNameCount = await userDb.SimpleDb.AsQueryable().Where(x => x.username == requestJson.username).CountAsync();
            if (userNameCount > 0)
            {
                await response.BadRequest("用户名已被使用，请选择其他用户名。");
                return;
            }

            var emailCount = await userDb.SimpleDb.AsQueryable().Where(x => x.email == requestJson.email).CountAsync();
            if (emailCount > 0)
            {
                await response.BadRequest("E-mail已被使用，请使用其他邮箱。");
                return;
            }

            //生成随机主题色
            var randomThemeColor = GetRandomThemeColor();
            var emailVerifyToken = Guid.NewGuid().ToString("n");
            emailVerifyToken += Guid.NewGuid().ToString("n");

            //生成SaltKey
            var saltKey = CryptoUtils.GenRandomIV();

            //插入数据库并清除缓存
            var user = new user
            {
                username = requestJson.username,
                email = requestJson.email,
                hashkey = saltKey,
                password = CryptoUtils.GetLoginHash(requestJson.pass, saltKey),
                roleid = 0,
                create_time = DateTime.Now,
                update_time = DateTime.Now,
                theme_color = randomThemeColor,
                info_key = emailVerifyToken
            };

            if (Config.Config.Options.EnableEmailVerify)
            {
                var uid = await userDb.SimpleDb.AsInsertable(user).RemoveDataCache().ExecuteReturnIdentityAsync();
                user.uid = uid;

                //Email验证Token存入Redis
                var verifyKey = cache.GetCacheKey($"emailVerify/{emailVerifyToken}");
                await cache.Put(verifyKey, user, 1200000); //20分钟有效期。

                //发送验证邮件
                var sendRes = await EmailSender.EmailVerify(user.email, emailVerifyToken);
                if (!sendRes)
                {
                    await response.JsonResponse(200, new UserRegResponse
                    {
                        status = 1,
                        is_send_email = 0,
                        message = "注册成功，但Email验证邮件发送失败了。"
                    });
                    return;
                }
            }
            else
            {
                user.roleid = 1; //不使用Email验证时，直接设置为已激活用户
                var uid = await userDb.SimpleDb.AsInsertable(user).RemoveDataCache().ExecuteReturnIdentityAsync();
                user.uid = uid;
            }

            //写入日志
            var loginLogDb = DbFactory.Get<LoginLog>();
            var loginLog = new login_log
            {
                create_time = DateTime.Now,
                email = user.email,
                ip = request.ContextItems["RealIp"].ToString(),
                proxy_ip = request.ContextItems["ForwardIp"].ToString(),
                useragent = request.ContextItems["UserAgent"].ToString(),
                uid = user.uid,
                username = user.username,
                userid = requestJson.userid,
                status = 11,
            };
            await loginLogDb.SimpleDb.AsInsertable(loginLog).ExecuteCommandAsync();

            //返回
            await response.JsonResponse(200, new UserRegResponse
            {
                status = 1,
                is_send_email = 1
            });
        }

        public static string GetRandomThemeColor()
        {
            //生成随机主题色，其中任何颜色的饱和度都应当较高
            //色表
            var colors = new List<string>
            {
                // Red
                "#F44336", "#E53935", "#D32F2F", "#C62828", "#FF5252",
                "#FF1744", "#F50057", "#D50000", "#FF6D6D", "#E53935",

                // Pink
                "#E91E63", "#EC407A", "#D81B60", "#C2185B", "#AD1457",
                "#F06292", "#FF4081", "#F50057", "#E91E63", "#FF80AB",

                // Purple
                "#9C27B0", "#BA68C8", "#AB47BC", "#8E24AA", "#7B1FA2",
                "#CE93D8", "#E040FB", "#D500F9", "#AA00FF", "#B388FF",

                // Deep Purple
                "#673AB7", "#9575CD", "#7E57C2", "#5E35B1", "#512DA8",
                "#B39DDB", "#7C4DFF", "#651FFF", "#6200EA", "#D1C4E9",

                // Indigo
                "#3F51B5", "#5C6BC0", "#3949AB", "#303F9F", "#283593",
                "#9FA8DA", "#536DFE", "#3D5AFE", "#304FFE", "#C5CAE9",

                // Blue
                "#2196F3", "#42A5F5", "#1E88E5", "#1976D2", "#1565C0",
                "#90CAF9", "#448AFF", "#2979FF", "#2962FF", "#82B1FF",

                // Light Blue
                "#03A9F4", "#29B6F6", "#039BE5", "#0288D1", "#0277BD",
                "#81D4FA", "#40C4FF", "#00B0FF", "#0091EA", "#80D8FF",

                // Cyan
                "#00BCD4", "#26C6DA", "#00ACC1", "#0097A7", "#00838F",
                "#80DEEA", "#18FFFF", "#00E5FF", "#00B8D4", "#84FFFF",

                // Teal
                "#009688", "#26A69A", "#00897B", "#00796B", "#00695C",
                "#80CBC4", "#64FFDA", "#1DE9B6", "#00BFA5", "#A7FFEB",

                // Green
                "#4CAF50", "#66BB6A", "#43A047", "#388E3C", "#2E7D32",
                "#A5D6A7", "#69F0AE", "#00E676", "#00C853", "#B9F6CA",

                // Light Green
                "#8BC34A", "#9CCC65", "#7CB342", "#689F38", "#558B2F",
                "#C5E1A5", "#B2FF59", "#76FF03", "#64DD17", "#CCFF90",

                // Lime
                "#CDDC39", "#D4E157", "#C0CA33", "#AFB42B", "#9E9D24",
                "#E6EE9C", "#EEFF41", "#C6FF00", "#AEEA00", "#F4FF81",

                // Yellow
                "#FFEB3B", "#FFEE58", "#FDD835", "#FBC02D", "#F9A825",
                "#FFF59D", "#FFFF00", "#FFEA00", "#FFD600", "#FFFF8D",

                // Amber
                "#FFC107", "#FFCA28", "#FFB300", "#FFA000", "#FF8F00",
                "#FFE082", "#FFD740", "#FFC400", "#FFAB00", "#FFE57F",

                // Orange
                "#FF9800", "#FFA726", "#FB8C00", "#F57C00", "#EF6C00",
                "#FFCC80", "#FFAB40", "#FF9100", "#FF6D00", "#FFD180",

                // Deep Orange
                "#FF5722", "#FF7043", "#F4511E", "#E64A19", "#D84315",
                "#FF8A65", "#FF3D00", "#DD2C00", "#FF6E40", "#FF9E80",

                // Brown
                "#795548", "#8D6E63", "#6D4C41", "#5D4037", "#4E342E",
                "#A1887F", "#BCAAA4", "#D7CCC8", "#3E2723", "#EFEBE9",

                // Gray
                "#9E9E9E", "#BDBDBD", "#757575", "#616161", "#424242",
                "#E0E0E0", "#F5F5F5", "#EEEEEE", "#212121", "#FAFAFA",

                // Blue Gray
                "#607D8B", "#78909C", "#546E7A", "#455A64", "#37474F",
                "#90A4AE", "#CFD8DC", "#ECEFF1", "#263238", "#B0BEC5"
            };

            var random = new Random();
            var index = random.Next(0, colors.Count - 1);
            return colors[index];
        }

        [HttpHandler("POST", "/user-email-activate")]
        public async Task UserEmailActivate(Request request, Response response)
        {
            if (!Config.Config.Options.EnableEmailVerify)
            {
                await response.BadRequest("未开启邮件验证，无需重发激活邮件。");
                return;
            }

            var requestJson = request.Json<EmailResetPassRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //验证码
            var cache = DbFactory.GetCache();
            var cKey = cache.GetCacheKey($"captcha_{requestJson.nonce}");
            var code = await cache.Get<string>(cKey);

            if (string.IsNullOrEmpty(code))
            {
                await response.BadRequest("验证码错误");
                return;
            }

            if (code.ToLower() != requestJson.code.ToLower())
            {
                await response.BadRequest("验证码错误");
                return;
            }

            var now = DateTime.Now;
            var emailTokenKey = cache.GetDataKey("tokenbucket/email_total_sender");
            var emailToken = await cache.RateLimiter(emailTokenKey, 20, Ccxc.Core.Utils.UnixTimestamp.GetTimestamp(now), 2000, 1);
            if (emailToken < 1)
            {
                await response.BadRequest("Email服务器：邮件发送太多，请稍后再试。");
                return;
            }

            var userTokenKey = cache.GetDataKey($"tokenbucket/user_{requestJson.userid}");
            var userToken = await cache.RateLimiter(userTokenKey, 1, Ccxc.Core.Utils.UnixTimestamp.GetTimestamp(now), 1440, 1);
            if (userToken < 1)
            {
                await response.BadRequest("Email服务器：重试太快，请稍后再试。");
                return;
            }

            //取出用户信息
            var userDb = DbFactory.Get<User>();
            var user = await userDb.SimpleDb.AsQueryable().Where(x => x.email == requestJson.email).FirstAsync();
            if (user == null)
            {
                await response.BadRequest("Email服务器：发送失败，请检查邮箱地址是否填写错误。");
                return;
            }

            var emailVerifyToken = Guid.NewGuid().ToString("n");
            emailVerifyToken += Guid.NewGuid().ToString("n");

            //用户认证信息写入数据库
            user.info_key = emailVerifyToken;
            user.update_time = DateTime.Now;
            await userDb.SimpleDb.AsUpdateable(user).UpdateColumns(x => new { x.info_key, x.update_time }).RemoveDataCache().ExecuteCommandAsync();


            //Email验证Token存入Redis
            var verifyKey = cache.GetCacheKey($"emailVerify/{emailVerifyToken}");
            await cache.Put(verifyKey, user, 1200000); //20分钟有效期。

            //发送验证邮件
            var sendRes = await EmailSender.EmailVerify(user.email, emailVerifyToken);
            if (!sendRes)
            {
                await response.BadRequest("Email服务器：发送失败，请检查邮箱地址是否填写错误，或稍后再试。");
                return;
            }

            //写入日志
            var loginLogDb = DbFactory.Get<LoginLog>();
            var loginLog = new login_log
            {
                create_time = now,
                email = user.email,
                ip = request.ContextItems["RealIp"].ToString(),
                proxy_ip = request.ContextItems["ForwardIp"].ToString(),
                useragent = request.ContextItems["UserAgent"].ToString(),
                uid = user.uid,
                username = user.username,
                userid = requestJson.userid,
                status = 11,
            };
            await loginLogDb.SimpleDb.AsInsertable(loginLog).ExecuteCommandAsync();

            //返回
            await response.OK();
        }

        [HttpHandler("POST", "/email-verify-check-token")]
        public async Task EmailVerifyCheckToken(Request request, Response response)
        {
            if (!Config.Config.Options.EnableEmailVerify)
            {
                await response.BadRequest("未开启邮件验证，无需进行邮件验证。");
                return;
            }

            var requestJson = request.Json<ResetPassCheckTokenRequest>();
            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var cache = DbFactory.GetCache();
            var verifyKey = cache.GetCacheKey($"emailVerify/{requestJson.token}");
            var user = await cache.Get<user>(verifyKey);

            if (user == null)
            {
                await response.BadRequest("邮件验证失败");
                return;
            }

            //提取满足条件的用户
            var userDb = DbFactory.Get<User>();
            var userItem = await userDb.SimpleDb.AsQueryable().Where(x => x.email == user.email && x.info_key == requestJson.token && x.roleid == 0).FirstAsync();

            if (userItem == null)
            {
                await response.BadRequest("用户不存在或当前用户无法进行邮件验证。请注意：如果你已经完成了邮件验证，请直接登录。");
                return;
            }

            //更新用户信息
            userItem.info_key = null;
            userItem.roleid = 1; //设置为已验证用户
            userItem.update_time = DateTime.Now;
            if (string.IsNullOrEmpty(userItem.theme_color))
            {
                userItem.theme_color = GetRandomThemeColor();
            }

            await userDb.SimpleDb.AsUpdateable(userItem)
                .UpdateColumns(x => new { x.info_key, x.roleid, x.update_time, x.theme_color })
                .RemoveDataCache().ExecuteCommandAsync();

            //清理Redis
            await cache.Delete(verifyKey);

            //写入日志
            var loginLogDb = DbFactory.Get<LoginLog>();
            var loginLog = new login_log
            {
                create_time = DateTime.Now,
                email = user.email,
                ip = request.ContextItems["RealIp"].ToString(),
                proxy_ip = request.ContextItems["ForwardIp"].ToString(),
                useragent = request.ContextItems["UserAgent"].ToString(),
                uid = user.uid,
                username = user.username,
                status = 12,
            };
            await loginLogDb.SimpleDb.AsInsertable(loginLog).ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/puzzle-check-ticket")]
        public async Task PuzzleCheckTicket(Request request, Response response)
        {
            var loginLogDb = DbFactory.Get<LoginLog>();
            var loginLog = new login_log
            {
                create_time = DateTime.Now,
                ip = request.ContextItems["RealIp"].ToString(),
                proxy_ip = request.ContextItems["ForwardIp"].ToString(),
                useragent = request.ContextItems["UserAgent"].ToString()
            };

            var requestJson = request.Json<CheckTicketRequest>();
            //判断请求是否有效
            if (!Validation.Valid(requestJson, out var reason))
            {
                loginLog.status = 2;
                loginLog.email = "";
                await loginLogDb.SimpleDb.AsInsertable(loginLog).ExecuteCommandAsync();

                await response.BadRequest(reason);
                return;
            }
            loginLog.email = $"Ticket: {requestJson.ticket}";

            //尝试根据Ticket取回Token
            var cache = DbFactory.GetCache();
            var ticketKey = cache.GetTempTicketKey(requestJson.ticket);

            var ticket = await cache.Get<PuzzleLoginTicketSession>(ticketKey);

            if (ticket == null)
            {
                loginLog.status = 7;
                await loginLogDb.SimpleDb.AsInsertable(loginLog).ExecuteCommandAsync();

                await response.BadRequest("验证失败，请返回首页检查登录状态。");
                return;
            }

            var userToken = ticket.token;
            if (string.IsNullOrEmpty(userToken))
            {
                loginLog.status = 7;
                await loginLogDb.SimpleDb.AsInsertable(loginLog).ExecuteCommandAsync();

                await response.BadRequest("User-Token获取失败，请返回首页检查登录状态。");
                return;
            }

            //销毁Ticket缓存
            await cache.Delete(ticketKey);

            //从User-Token中恢复Session
            var sessionKey = cache.GetUserSessionKey(userToken);
            var userSession = await cache.Get<UserSession>(sessionKey);

            if (userSession == null || userSession.is_active != 1) //Session不存在
            {
                loginLog.status = 8;
                await loginLogDb.SimpleDb.AsInsertable(loginLog).ExecuteCommandAsync();

                await response.Unauthorized("登录已经过期，请返回首页检查登录状态。");
                return;
            }

            var openType = 0;

            //取得该用户GID
            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == userSession.uid).FirstAsync();
            if (groupBindItem == null)
            {
                //无组队，open_type只能为0
            }
            else
            {

                var gid = groupBindItem.gid;

                //取得进度
                var progressDb = DbFactory.Get<Progress>();
                var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            }

            //返回给前端足以让前端恢复User-Token登录状态的信息
            loginLog.status = 6;
            loginLog.username = userSession.username;
            loginLog.uid = userSession.uid;
            await loginLogDb.SimpleDb.AsInsertable(loginLog).ExecuteCommandAsync();

            await response.JsonResponse(200, new PuzzleCheckTicketResponse
            {
                status = 1,
                user_login_info = new UserLoginResponse.UserLoginInfo
                {
                    uid = userSession.uid,
                    username = userSession.username,
                    roleid = userSession.roleid,
                    token = userSession.token,
                    sk = userSession.sk,
                    etc = userSession.is_betaUser == 1 ? "52412" : "10000"
                },
                open_type = openType
            });
        }

        [HttpHandler("POST", "/user-login")]
        public async Task UserLogin(Request request, Response response)
        {
            var loginLogDb = DbFactory.Get<LoginLog>();
            var loginLog = new login_log
            {
                create_time = DateTime.Now,
                ip = request.ContextItems["RealIp"].ToString(),
                proxy_ip = request.ContextItems["ForwardIp"].ToString(),
                useragent = request.ContextItems["UserAgent"].ToString()
            };

            var requestJson = request.Json<UserLoginRequest>();
            loginLog.email = requestJson.email;
            loginLog.userid = requestJson.userid;

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out var reason))
            {
                loginLog.status = 2;
                await loginLogDb.SimpleDb.AsInsertable(loginLog).ExecuteCommandAsync();

                await response.BadRequest(reason);
                return;
            }

            var cache = DbFactory.GetCache();
            var cKey = cache.GetCacheKey($"captcha_{requestJson.nonce}");
            var code = await cache.Get<string>(cKey);

            if (string.IsNullOrEmpty(code))
            {
                await response.BadRequest("验证码错误");
                return;
            }

            if (code.ToLower() != requestJson.code.ToLower())
            {
                await response.BadRequest("验证码错误");
                return;
            }

            //数据库对象
            var userDb = DbFactory.Get<User>();
            var userItem = await userDb.SimpleDb.AsQueryable().Where(it => it.email == requestJson.email).FirstAsync();

            if (userItem == null)
            {
                //用户不存在
                loginLog.status = 3;
                await loginLogDb.SimpleDb.AsInsertable(loginLog).ExecuteCommandAsync();

                await response.BadRequest("用户名或密码错误");
                return;
            }

            loginLog.uid = userItem.uid;
            loginLog.username = userItem.username;

            var hashedPass = CryptoUtils.GetLoginHash(requestJson.pass, userItem.hashkey);
            if (hashedPass != userItem.password)
            {
                //密码错误
                loginLog.status = 4;
                await loginLogDb.SimpleDb.AsInsertable(loginLog).ExecuteCommandAsync();

                await response.BadRequest("用户名或密码错误");
                return;
            }

            if (userItem.roleid == 0)
            {
                //需要激活
                loginLog.status = 31;
                await loginLogDb.SimpleDb.AsInsertable(loginLog).ExecuteCommandAsync();


                await response.JsonResponse(403, new BasicResponse
                {
                    status = 31,
                    message = "您还未成为本站正式用户。请尝试通过邮箱认证激活账号。"
                });
                return;
            }

            if (userItem.roleid < 0)
            {
                //被封禁
                loginLog.status = 5;
                await loginLogDb.SimpleDb.AsInsertable(loginLog).ExecuteCommandAsync();

                await response.BadRequest("您的账号目前无法登录，");
                return;
            }

            //查询该用户是否已加入队伍
            var gid = 0;
            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == userItem.uid).FirstAsync();

            if (groupBindItem != null)
            {
                gid = groupBindItem.gid;
            }

            //创建新Session
            var uuid = Guid.NewGuid().ToString("n");
            var sk = CryptoUtils.GetRandomKey();

            var newSession = new UserSession
            {
                uid = userItem.uid,
                gid = gid,
                username = userItem.username,
                roleid = userItem.roleid,
                color = userItem.theme_color,
                third_pron = UserSession.GetThirdPron(userItem.gender, userItem.third_pron),
                token = uuid,
                sk = sk,
                login_time = DateTime.Now,
                last_update = DateTime.Now,
                is_active = 1,
                is_betaUser = (userItem.info_key == "beta_user") ? 1 : 0 //若info_key内容为beta_user，则授予测试用户权限
            };

            //从用户Session列表中读取当前Session列表
            var userSessionsKey = cache.GetUserSessionStorage(userItem.uid);
            var userSessions = await cache.Get<List<string>>(userSessionsKey);

            //检查已有的Session数量+1是否超过限制
            var maxSession = Config.SystemConfigLoader.Config.UserSessionMaxCount;
            userSessions ??= [];
            if (userSessions.Count + 1 > maxSession)
            {
                //让最早的Session失效
                var firstSession = userSessions[0];
                var firstSessionKey = cache.GetUserSessionKey(firstSession);
                var firstSessionItem = await cache.Get<UserSession>(firstSessionKey);
                firstSessionItem.is_active = 0;
                firstSessionItem.inactive_message = $"您的账号已于{DateTime.Now:yyyy-MM-dd HH:mm}在另一地点登录，超过最大登录数量限制。";
                await cache.Put(firstSessionKey, firstSessionItem, Config.SystemConfigLoader.Config.UserSessionTimeout * 1000);

                userSessions.RemoveAt(0);
            }
            userSessions.Add(uuid);
            await cache.Put(userSessionsKey, userSessions, 30L*86400000);

            //保存当前Session
            var sessionKey = cache.GetUserSessionKey(uuid);
            await cache.Put(sessionKey, newSession, Config.SystemConfigLoader.Config.UserSessionTimeout * 1000);

            loginLog.status = 1;
            await loginLogDb.SimpleDb.AsInsertable(loginLog).ExecuteCommandAsync();

            //将uid, username, roleid, token, sk返回给前端
            await response.JsonResponse(200, new UserLoginResponse
            {
                status = 1,
                user_login_info = new UserLoginResponse.UserLoginInfo
                {
                    uid = userItem.uid,
                    username = userItem.username,
                    roleid = userItem.roleid,
                    token = uuid,
                    sk = sk,
                    etc = userItem.info_key == "beta_user" ? "52412" : "10000",
                    color = userItem.theme_color
                }
            });
        }

        [HttpHandler("POST", "/user-logout")]
        public async Task UserLogout(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Normal);
            if (userSession == null) return;

            var cache = DbFactory.GetCache();
            var sessionKey = cache.GetUserSessionKey(userSession.token);
            await cache.Delete(sessionKey);

            await response.OK();
        }

        [HttpHandler("POST", "/modify-password")]
        public async Task ModifyPassword(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Normal);
            if (userSession == null) return;

            var requestJson = request.Json<ModifyPasswordRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //验证码
            var cache = DbFactory.GetCache();
            var cKey = cache.GetCacheKey($"captcha_{requestJson.nonce}");
            var code = await cache.Get<string>(cKey);

            if (string.IsNullOrEmpty(code))
            {
                await response.BadRequest("验证码错误");
                return;
            }

            if (code.ToLower() != requestJson.code.ToLower())
            {
                await response.BadRequest("验证码错误");
                return;
            }

            //取出当前用户信息
            var userDb = DbFactory.Get<User>();

            var user = await userDb.SimpleDb.AsQueryable().Where(it => it.uid == userSession.uid).FirstAsync();
            if(user == null || user.roleid < 1)
            {
                await response.Unauthorized("用户不存在或不允许当前用户进行操作。");
                return;
            }

            //验证原密码
            var oldPass = CryptoUtils.GetLoginHash(requestJson.old_pass, user.hashkey);
            if(oldPass != user.password)
            {
                await response.BadRequest("原密码不正确。");
                return;
            }

            //新密码写入数据库
            user.hashkey = CryptoUtils.GenRandomIV();
            user.password = CryptoUtils.GetLoginHash(requestJson.pass, user.hashkey);
            user.update_time = DateTime.Now;

            await userDb.SimpleDb.AsUpdateable(user).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/edit-user")]
        public async Task EditUser(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Normal);
            if (userSession == null) return;

            var requestJson = request.Json<EditUserRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out var reason))
            {
                await response.BadRequest(reason);
                return;
            }

            if (string.IsNullOrWhiteSpace(requestJson.username))
            {
                await response.BadRequest("用户名不能是空白字符");
                return;
            }

            if (requestJson.username.Length > 25)
            {
                await response.BadRequest("用户名长度过于长了");
                return;
            }

            if ((requestJson.third_pron?.Length ?? 0) > 10)
            {
                await response.BadRequest("第三人称代词长度过长，请检查输入。");
                return;
            }

            //取出当前用户信息
            var userDb = DbFactory.Get<User>();
            var userList = await userDb.SimpleDb.AsQueryable().WithCache().ToListAsync();

            var user = await userDb.SimpleDb.AsQueryable().Where(it => it.uid == userSession.uid).FirstAsync();
            if (user == null || user.roleid < 1)
            {
                await response.Unauthorized("用户不存在或不允许当前用户进行操作。");
                return;
            }

            var loginInfoUpdate = false;
            //判断是否重复
            if (user.username != requestJson.username)
            {
                var userNameSet = new HashSet<string>(userList.Select(it => it.username.ToLower()));
                if (userNameSet.Contains(requestJson.username.ToLower()))
                {
                    await response.BadRequest("用户名已被使用，请选择其他用户名。");
                    return;
                }

                user.username = requestJson.username;
                loginInfoUpdate = true;
            }

            //写入新信息
            user.phone = requestJson.phone;

            var newProfileString = requestJson.profile;
            if(newProfileString != null && newProfileString.Length > 350)
            {
                newProfileString = newProfileString.Substring(0, 350);
            }
            user.profile = newProfileString;
            user.theme_color = requestJson.theme_color;
            user.update_time = DateTime.Now;
            user.gender = requestJson.gender;
            user.third_pron = requestJson.third_pron;

            await userDb.SimpleDb.AsUpdateable(user).RemoveDataCache().ExecuteCommandAsync();

            if (loginInfoUpdate)
            {
                await response.JsonResponse(200, new BasicResponse
                {
                    status = 13,
                    message = "您修改了登录信息，请重新登录。"
                });
            }
            else
            {
                await response.OK();
            }
        }
    }
}
