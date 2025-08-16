using Ccxc.Core.HttpServer;
using ccxc_backend.Controllers.Announcements;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using ccxc_backend.Functions;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Admin
{
    [Export(typeof(HttpController))]
    public class PuzzleScriptAdminController : HttpController
    {
        [HttpHandler("POST", "/admin/add-puzzle-script")]
        public async Task AddPuzzleScript(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<PuzzleScriptRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var now = DateTime.Now;

            var a = new puzzle_backend_script
            {
                key = requestJson.key,
                desc = requestJson.desc,
                script = requestJson.script,
                length = requestJson.script.Length,
                dt_create = now,
                dt_update = now,
            };

            //插入新文章
            var bsDb = DbFactory.Get<PuzzleBackendScript>();
            await bsDb.SimpleDb.AsInsertable(a).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/delete-puzzle-script")]
        public async Task DeletePuzzleScript(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<DeletePuzzleScriptRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //删除它
            var bsDb = DbFactory.Get<PuzzleBackendScript>();
            await bsDb.SimpleDb.AsDeleteable().Where(it => it.psid == requestJson.psid).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/edit-puzzle-script")]
        public async Task EditPuzzleScript(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<EditPuzzleScriptRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //生成修改后对象
            var now = DateTime.Now;
            var updateScript = new puzzle_backend_script
            {
                psid = requestJson.psid,
                key = requestJson.key,
                desc = requestJson.desc,
                script = requestJson.script,
                length = requestJson.script.Length,
                dt_update = now,
            };

            var bsDb = DbFactory.Get<PuzzleBackendScript>();
            await bsDb.SimpleDb.AsUpdateable(updateScript).IgnoreColumns(it => new { it.dt_create }).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/list-puzzle-script")]
        public async Task ListPuzzleScript(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var bsDb = DbFactory.Get<PuzzleBackendScript>();

            //获得文章列表
            var bsList = await bsDb.SimpleDb.AsQueryable().OrderBy(x => x.key).WithCache().ToListAsync();

            await response.JsonResponse(200, new ListPuzzleScriptResponse
            {
                status = 1,
                puzzle_scripts = bsList,
            });
        }

        [HttpHandler("POST", "/admin/get-ai-script-enable")]
        public async Task GetAIScriptEnable(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            await response.JsonResponse(200, new {
                status = 1,
                enable = Config.SystemConfigLoader.Config.AdminAiEnable,
            });
        }

        [HttpHandler("POST", "/admin/ai-script-completion")]
        public async Task AIScriptCompletion(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<AIScriptCompletionRequest>();

            var completionMetadata = requestJson.completionMetadata;
            if (completionMetadata == null)
            {
                await response.JsonResponse(200, new AIScriptCompletionResponse
                {
                    status = 1,
                    completion = ""
                });
            }

            if (Config.SystemConfigLoader.Config.AdminAiEnable == 0)
            {
                await response.BadRequest("AI自动补全代码功能未启用。");
                return;
            }

            var ai = AIService.Instance;
            var completion = await ai.GetCodeCompletion(completionMetadata.language, completionMetadata.textBeforeCursor, completionMetadata.textAfterCursor);

            await response.JsonResponse(200, new AIScriptCompletionResponse
            {
                status = 1,
                completion = completion,
            });
        }
    }
}
