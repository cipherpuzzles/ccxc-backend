﻿using Ccxc.Core.HttpServer;
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
    public class PuzzleGroupAdminController : HttpController
    {
        [HttpHandler("POST", "/admin/add-puzzle-group")]
        public async Task AddPuzzleGroup(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<AddPuzzleGroupRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //插入新分区
            var pgDb = DbFactory.Get<PuzzleGroup>();
            var newPg = new puzzle_group
            {
                pg_name = requestJson.pg_name,
                pg_desc = requestJson.pg_desc,
                is_hide = (byte)(requestJson.is_hide == 1 ? 1 : 0)
            };

            await pgDb.SimpleDb.AsInsertable(newPg).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/delete-puzzle-group")]
        public async Task DeletePuzzleGroup(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<DeletePuzzleGroupRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //删除它
            var pgDb = DbFactory.Get<PuzzleGroup>();
            await pgDb.SimpleDb.AsDeleteable().Where(it => it.pgid == requestJson.pgid).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/edit-puzzle-group")]
        public async Task EditPuzzleGroup(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<EditPuzzleGroupRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //生成修改后对象
            var updatePg = new puzzle_group
            {
                pgid = requestJson.pgid,
                pg_name = requestJson.pg_name,
                pg_desc = requestJson.pg_desc,
                is_hide = (byte)(requestJson.is_hide == 1 ? 1 : 0)
            };

            var pgDb = DbFactory.Get<PuzzleGroup>();
            await pgDb.SimpleDb.AsUpdateable(updatePg).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/get-puzzle-group")]
        public async Task GetPuzzleGroup(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var pgDb = DbFactory.Get<PuzzleGroup>();
            var pgList = await pgDb.SimpleDb.AsQueryable().OrderBy(it => it.pgid).WithCache().ToListAsync();

            await response.JsonResponse(200, new GetPuzzleGroupResponse
            {
                status = 1,
                puzzle_group = pgList.ToList()
            });
        }

        [HttpHandler("POST", "/admin/set-avaliable-group-id")]
        public async Task SetAvaliableGroupId(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<SetAvaliableGroupIdRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //修改缓存的值
            var cache = DbFactory.GetCache();
            var openedGroupKey = cache.GetDataKey("opened-groups");
            await cache.Put(openedGroupKey, requestJson.value);

            await response.OK();
        }

        [HttpHandler("POST", "/admin/update-tips-cost")]
        public async Task GetTipsCost(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<SetTipsCostRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var cache = DbFactory.GetCache();

            //修改提示币价格
            var tipsCostDefaultKey = cache.GetDataKey("tips-cost-default");
            var tipsCostMetaKey = cache.GetDataKey("tips-cost-meta");

            await cache.Put(tipsCostDefaultKey, requestJson.tips_cost_default);
            await cache.Put(tipsCostMetaKey, requestJson.tips_cost_meta);

            await response.OK();
        }
    }
}
