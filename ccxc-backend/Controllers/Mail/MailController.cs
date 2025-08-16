﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ccxc.Core.HttpServer;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using SqlSugar;

namespace ccxc_backend.Controllers.Mail
{
    [Export(typeof(HttpController))]
    public class MailController : HttpController
    {
        [HttpHandler("POST", "/send-mail")]
        public async Task SendMail(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            var requestJson = request.Json<SendMailRequest>();

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

            var content = requestJson.content;
            const string regExStyle = "<style[^>]*?>[\\s\\S]*?<\\/style>";
            const string regExScript = "<script[^>]*?>[\\s\\S]*?<\\/script>";
            const string regExHtml = "<[^>]+>";

            content = Regex.Replace(content, regExStyle, "", RegexOptions.IgnoreCase);
            content = Regex.Replace(content, regExScript, "", RegexOptions.IgnoreCase);
            content = Regex.Replace(content, regExHtml, "");

            //写入新消息
            var newMessage = new message
            {
                content = content,
                update_time = DateTime.Now,
                create_time = DateTime.Now,
                gid = gid,
                uid = userSession.uid,
                is_read = 0,
                direction = 0
            };

            var messageDb = DbFactory.Get<Message>();
            await messageDb.SimpleDb.AsInsertable(newMessage).ExecuteCommandAsync();
            await response.OK();
        }

        [HttpHandler("POST", "/get-mail")]
        public async Task GetMail(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            var requestJson = request.Json<GetMailRequest>();

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

            //读取当前GID的所有邮件
            var messageDb = DbFactory.Get<Message>();
            const int pageSize = 5;
            var totalCount = new RefAsync<int>(0);
            var messageList = await messageDb.SimpleDb.AsQueryable().Where(it => it.gid == gid)
                .OrderBy(it => it.mid, OrderByType.Desc)
                .ToPageListAsync(requestJson.page, pageSize, totalCount);

            //生成结果
            var userDb = DbFactory.Get<User>();
            var userDict = (await userDb.SimpleDb.AsQueryable().Select(x => new { x.uid, x.username, x.roleid }).WithCache().ToListAsync()).ToDictionary(it => it.uid, it => it);
            var resList = messageList.Select(it =>
            {
                var r = new MessageView(it);
                if (!userDict.ContainsKey(r.uid)) return r;

                var u = userDict[r.uid];
                r.user_name = u.username;
                r.roleid = u.roleid;
                return r;
            }).ToList();

            var res = new GetMailResponse
            {
                status = 1,
                page = requestJson.page,
                page_size = pageSize,
                total_count = totalCount.Value,
                messages = resList
            };

            //标记已读并写回
            var rewriteList = messageList.Where(it => it.is_read == 0 && it.direction == 1).ToList();
            if (rewriteList.Count > 0)
            {
                rewriteList.ForEach(it => it.is_read = 1);
                await messageDb.SimpleDb.AsUpdateable(rewriteList).UpdateColumns(it => new { it.is_read }).ExecuteCommandAsync();
            }
            

            //返回
            await response.JsonResponse(200, res);
        }
    }
}
