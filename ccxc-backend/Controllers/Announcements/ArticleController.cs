using Ccxc.Core.HttpServer;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Announcements
{
    [Export(typeof(HttpController))]
    public class ArticleController : HttpController
    {
        [HttpHandler("POST", "/admin/add-article")]
        public async Task AddArticle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<ArticleRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            if (string.IsNullOrEmpty(requestJson.path))
            {
                requestJson.path = requestJson.title;
            }

            var now = DateTime.Now;

            var a = new article
            {
                title = requestJson.title,
                path = requestJson.path,
                content = requestJson.content,
                order = requestJson.order,
                dt_create = now,
                dt_update = now,
                is_hide = requestJson.is_hide,
            };

            //插入新文章
            var artDb = DbFactory.Get<Article>();
            await artDb.SimpleDb.AsInsertable(a).RemoveDataCache("article").ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/delete-article")]
        public async Task DeleteArticle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<DeleteArticleRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //删除它
            var artDb = DbFactory.Get<Article>();
            await artDb.SimpleDb.AsDeleteable().Where(it => it.aid == requestJson.aid).RemoveDataCache("article").ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/edit-article")]
        public async Task EditArticle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<EditArticleRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            if (string.IsNullOrEmpty(requestJson.path))
            {
                requestJson.path = requestJson.title;
            }

            //生成修改后对象
            var updateArticle = new article
            {
                aid = requestJson.aid,
                order = requestJson.order,
                title = requestJson.title,
                content = requestJson.content,
                path = requestJson.path,
                dt_update = DateTime.Now,
                is_hide = requestJson.is_hide
            };

            var artDb = DbFactory.Get<Article>();
            await artDb.SimpleDb.AsUpdateable(updateArticle).IgnoreColumns(it => new { it.dt_create }).RemoveDataCache("article").ExecuteCommandAsync();

            await response.OK();
        }


        [HttpHandler("POST", "/list-article")]
        public async Task ListArticle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var artDb = DbFactory.Get<Article>();

            //获得文章列表
            var artList = await artDb.SimpleDb.AsQueryable()
                .OrderBy(x => x.order).OrderByDescending(x => x.dt_create)
                .WithCache().ToListAsync();

            await response.JsonResponse(200, new ListArticleResponse
            {
                status = 1,
                articles = artList,
            });
        }

        [HttpHandler("POST", "/get-article")]
        public async Task GetArticle(Request request, Response response)
        {
            var requestJson = request.Json<GetArticleRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var artDb = DbFactory.Get<Article>();

            //获得指定path的文章
            var article = await artDb.SimpleDb.AsQueryable().Where(it => it.path == requestJson.path).WithCache().FirstAsync();

            if (article == null)
            {
                await response.JsonResponse(404, new GetArticleResponse
                {
                    status = 2,
                    message = "文章不存在"
                });
                return;
            }

            //获得文章列表
            var artList = await artDb.SimpleDb.AsQueryable().Where(x => x.is_hide == 0)
                .OrderBy(x => x.order).OrderByDescending(x => x.dt_create)
                .Select(x => new ArticleSimpleView { path = x.path, title = x.title }).WithCache().ToListAsync();

            await response.JsonResponse(200, new GetArticleResponse
            {
                status = 1,
                article = article,
                list = artList
            });
        }
    }
}
