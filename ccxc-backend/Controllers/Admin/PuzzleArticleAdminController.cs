using Ccxc.Core.HttpServer;
using ccxc_backend.Controllers.Announcements;
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
    public class PuzzleArticleAdminController : HttpController
    {
        [HttpHandler("POST", "/admin/add-puzzle-article")]
        public async Task AddPuzzleArticle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<PuzzleArticleRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var now = DateTime.Now;

            var a = new puzzle_article
            {
                key = requestJson.key,
                title = requestJson.title,
                content = requestJson.content,
                dt_create = now,
                dt_update = now,
                is_hide = requestJson.is_hide,
            };

            //插入新文章
            var artDb = DbFactory.Get<PuzzleArticle>();
            await artDb.SimpleDb.AsInsertable(a).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/delete-puzzle-article")]
        public async Task DeletePuzzleArticle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<DeletePuzzleArticleRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //删除它
            var artDb = DbFactory.Get<PuzzleArticle>();
            await artDb.SimpleDb.AsDeleteable().Where(it => it.paid == requestJson.paid).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/edit-puzzle-article")]
        public async Task EditPuzzleArticle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<EditPuzzleArticleRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //生成修改后对象
            var updateArticle = new puzzle_article
            {
                paid = requestJson.paid,
                key = requestJson.key,
                title = requestJson.title,
                content = requestJson.content,
                dt_update = DateTime.Now,
                is_hide = requestJson.is_hide
            };

            var artDb = DbFactory.Get<PuzzleArticle>();
            await artDb.SimpleDb.AsUpdateable(updateArticle).IgnoreColumns(it => new { it.dt_create }).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/list-puzzle-article")]
        public async Task ListPuzzleArticle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var artDb = DbFactory.Get<PuzzleArticle>();

            //获得文章列表
            var artList = await artDb.SimpleDb.AsQueryable().OrderBy(x => x.dt_create).WithCache().ToListAsync();

            await response.JsonResponse(200, new ListPuzzleArticleResponse
            {
                status = 1,
                puzzle_articles = artList,
            });
        }
    }
}
