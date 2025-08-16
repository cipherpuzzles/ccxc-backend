using Ccxc.Core.HttpServer;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using ccxc_backend.Functions;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Admin
{
    [Export(typeof(HttpController))]
    public class OracleAdminController : HttpController
    {
        [HttpHandler("POST", "/admin/query-oracle")]
        public async Task QueryOracle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<QueryOracleAdminRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var messageDb = DbFactory.Get<DataModels.Oracle>();

            const int pageSize = 20;
            var page = requestJson.page;
            if (page <= 0) page = 1;
            var totalCount = new RefAsync<int>(0);

            var orderType = requestJson.order == 0 ? OrderByType.Desc : OrderByType.Asc;

            var replyType = requestJson.reply switch
            {
                2 => 0,
                1 => 1,
                _ => 0
            };

            var resultList = await messageDb.SimpleDb.AsQueryable()
                .WhereIF(requestJson.reply != 0, it => it.is_reply == replyType)
                .WhereIF(requestJson.gid?.Count > 0, it => requestJson.gid.Contains(it.gid))
                .WhereIF(requestJson.pid?.Count > 0, it => requestJson.pid.Contains(it.pid))
                .OrderBy(it => it.create_time, orderType)
                .ToPageListAsync(page, pageSize, totalCount);

            //生成结果
            var groupDb = DbFactory.Get<UserGroup>();
            var groupNameDict = (await groupDb.SimpleDb.AsQueryable().WithCache().ToListAsync()).ToDictionary(it => it.gid, it => it.groupname);
            var puzzleDb = DbFactory.Get<Puzzle>();
            var puzzleDict = (await puzzleDb.SimpleDb.AsQueryable().WithCache().ToListAsync()).ToDictionary(it => it.pid, it => it);

            var unlockDelay = RedisNumberCenter.ManualTipReplyDelay;
            var resList = resultList.Select(it =>
            {
                var r = new OracleView(it)
                {
                    unlock_time = it.create_time.AddMinutes(unlockDelay)
                };
                if (!groupNameDict.ContainsKey(r.gid)) return r;
                r.group_name = groupNameDict[r.gid];
                if (!puzzleDict.ContainsKey(r.pid)) return r;
                r.pgid = puzzleDict[r.pid].pgid;
                r.puzzle_title = puzzleDict[r.pid].title;
                return r;
            }).ToList();

            var res = new QueryOracleResponse
            {
                status = 1,
                page = requestJson.page,
                page_size = pageSize,
                total_count = totalCount.Value,
                oracles = resList
            };
            await response.JsonResponse(200, res);
        }

        [HttpHandler("POST", "/admin/reply-oracle")]
        public async Task ReplyOracle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<OracleReplyRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var oracleDb = DbFactory.Get<DataModels.Oracle>();
            var oracle = await oracleDb.SimpleDb.AsQueryable().Where(it => it.oracle_id == requestJson.oracle_id).FirstAsync();
            if (oracle == null)
            {
                await response.BadRequest("oracle not found");
                return;
            }

            oracle.is_reply = 1;
            oracle.reply_time = DateTime.Now;
            oracle.reply_content = requestJson.reply_content;
            oracle.extend_function = string.Join(",", requestJson.extend_function);
            await oracleDb.SimpleDb.AsUpdateable(oracle).UpdateColumns(x => new { x.is_reply, x.reply_time, x.reply_content, x.extend_function }).ExecuteCommandAsync();

            await response.OK();
        }
    }
}
