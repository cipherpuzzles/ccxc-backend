using Ccxc.Core.HttpServer;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Announcements
{
    public class AnnoIdCache
    {
        public int aid { get; set; }
        public int is_hide { get; set; }
    }

    [Export(typeof(HttpController))]
    public class AnnoController : HttpController
    {
        [HttpHandler("POST", "/admin/add-announcement")]
        public async Task AddAnnouncement(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<AddAnnoRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //插入新公告
            var annoDb = DbFactory.Get<Announcement>();

            var now = DateTime.Now;

            var newAnnouncement = new announcement
            {
                create_time = now,
                update_time = now,
                content = requestJson.content,
                is_hide = requestJson.is_hide
            };

            await annoDb.SimpleDb.AsInsertable(newAnnouncement).RemoveDataCache().ExecuteCommandAsync();
            await ResetAnnoCache(annoDb);

            await response.OK();
        }

        public static async Task ResetAnnoCache(Announcement annoDb)
        {
            var annoIdList = await annoDb.SimpleDb.AsQueryable().Select(x => new AnnoIdCache { aid = x.aid, is_hide = x.is_hide }).ToListAsync();
            var annoIdSet = annoIdList.GroupBy(x => x.is_hide).ToDictionary(x => x.Key, x => new HashSet<int>(x.Select(y => y.aid)));
            var key = "ccxc:datacache:announcement_id_cache";

            var cache = DbFactory.GetCache();
            await cache.Put(key, annoIdSet);
        }

        [HttpHandler("POST", "/admin/delete-announcement")]
        public async Task DeleteAnnouncement(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<DeleteAnnoRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //删除它
            var annoDb = DbFactory.Get<Announcement>();
            await annoDb.SimpleDb.AsDeleteable().Where(it => it.aid == requestJson.aid).RemoveDataCache().ExecuteCommandAsync();
            await ResetAnnoCache(annoDb);

            await response.OK();
        }

        [HttpHandler("POST", "/admin/edit-announcement")]
        public async Task EditAnnouncement(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<EditAnnoRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //生成修改后对象
            var updateAnno = new announcement
            {
                aid = requestJson.aid,
                content = requestJson.content,
                update_time = DateTime.Now
            };

            var annoDb = DbFactory.Get<Announcement>();
            await annoDb.SimpleDb.AsUpdateable(updateAnno).IgnoreColumns(it => new { it.create_time }).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/get-announcement")]
        public async Task AdminGetAnnouncement(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<GetAnnoRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var annoDb = DbFactory.Get<Announcement>();
            IEnumerable<announcement> annoList = await annoDb.SimpleDb.AsQueryable().WithCache().ToListAsync();

            if (requestJson.aids != null && requestJson.aids.Count > 0)
            {
                var aidSet = new HashSet<int>(requestJson.aids);
                annoList = annoList.Where(it => aidSet.Contains(it.aid));
            }

            annoList = annoList.OrderByDescending(it => it.create_time);

            await response.JsonResponse(200, new
            {
                status = 1,
                announcements = annoList.ToList()
            });
        }

        [HttpHandler("POST", "/get-announcement")]
        public async Task GetAnnouncement(Request request, Response response)
        {
            var requestJson = request.Json<GetAnnoRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var annoDb = DbFactory.Get<Announcement>();
            IEnumerable<announcement> annoList = await annoDb.SimpleDb.AsQueryable().Where(x => x.is_hide != 1).WithCache().ToListAsync();

            if (requestJson.aids != null && requestJson.aids.Count > 0)
            {
                var aidSet = new HashSet<int>(requestJson.aids);
                annoList = annoList.Where(it => aidSet.Contains(it.aid));
            }

            annoList = annoList.OrderByDescending(it => it.create_time);

            //尝试取出用户Session
            IDictionary<string, object> headers = request.Header;
            if (headers.ContainsKey("user-token"))
            {
                var token = headers["user-token"].ToString();

                var cache = DbFactory.GetCache();
                var sessionKey = cache.GetUserSessionKey(token);
                var userSession = await cache.Get<UserSession>(sessionKey);

                if (userSession != null)
                {
                    var uid = userSession.uid;

                    //更新用户阅读过的公告ID
                    if (annoList?.Count() > 0)
                    {
                        var maxReadAnnoKey = cache.GetCacheKey($"read_anno_id_for_{uid}");
                        await cache.Put(maxReadAnnoKey, new HashSet<int>(annoList.Select(x => x.aid)));
                    }
                }
            }

            await response.JsonResponse(200, new GetAnnoResponse
            {
                status = 1,
                announcements = annoList.Select(x => new AnnoItem(x)).ToList()
            });
        }
    }
}
