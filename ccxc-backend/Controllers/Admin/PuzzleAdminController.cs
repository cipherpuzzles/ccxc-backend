using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ccxc.Core.HttpServer;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;

namespace ccxc_backend.Controllers.Admin
{
    [Export(typeof(HttpController))]
    public class PuzzleAdminController : HttpController
    {
        [HttpHandler("POST", "/admin/add-puzzle")]
        public async Task AddPuzzle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<AddPuzzleRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }
            var puzzleDb = DbFactory.Get<Puzzle>();
            if (requestJson.pid == 0)
            {
                //获取最大pid
                var maxPid = await puzzleDb.SimpleDb.AsQueryable().MaxAsync(p => p.pid);
                requestJson.pid = maxPid + 1;
            }

            //插入新题目

            var newPuzzle = new puzzle
            {
                pid = requestJson.pid,
                pgid = requestJson.pgid,
                desc = requestJson.desc,
                type = requestJson.type,
                title = requestJson.title,
                author = requestJson.author,
                content = requestJson.content,
                image = requestJson.image,
                html = requestJson.html,
                script = requestJson.script,
                answer_type = requestJson.answer_type,
                answer = requestJson.answer,
                check_answer_type = requestJson.check_answer_type,
                check_answer_function = requestJson.check_answer_function,
                attempts_count = requestJson.attempts_count,
                jump_keyword = requestJson.jump_keyword,
                extend_content = requestJson.extend_content,
                extend_data = requestJson.extend_data,
                analysis = requestJson.analysis,
                dt_update = DateTime.Now
            };

            await puzzleDb.SimpleDb.AsInsertable(newPuzzle).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/delete-puzzle")]
        public async Task DeletePuzzle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<DeletePuzzleRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //删除它
            var puzzleDb = DbFactory.Get<Puzzle>();
            await puzzleDb.SimpleDb.AsDeleteable().Where(it => it.pid == requestJson.pid).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/edit-puzzle")]
        public async Task EditPuzzle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<AddPuzzleRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            if (requestJson.last_dt_update == DateTime.MinValue)
            {
                await response.BadRequest("last_dt_update is required");
                return;
            }

            //读取原题目
            var puzzleDb = DbFactory.Get<Puzzle>();
            var puzzle = await puzzleDb.SimpleDb.AsQueryable().FirstAsync(it => it.pid == requestJson.pid);

            if (Math.Abs((puzzle.dt_update - requestJson.last_dt_update).TotalMilliseconds) > 1)
            {
                await response.BadRequest($"编辑冲突。当前提交基于{requestJson.last_dt_update:yyyy-MM-dd HH:mm:ss}版本。而服务器最新版为{puzzle.dt_update:yyyy-MM-dd HH:mm:ss}。请注意备份当前内容，刷新后重试。");
                return;
            }

            //生成修改后对象
            puzzle.pgid = requestJson.pgid;
            puzzle.desc = requestJson.desc;
            puzzle.type = requestJson.type;
            puzzle.title = requestJson.title;
            puzzle.author = requestJson.author;
            puzzle.content = requestJson.content;
            puzzle.image = requestJson.image;
            puzzle.html = requestJson.html;
            puzzle.script = requestJson.script;
            puzzle.answer_type = requestJson.answer_type;
            puzzle.answer = requestJson.answer;
            puzzle.check_answer_type = requestJson.check_answer_type;
            puzzle.check_answer_function = requestJson.check_answer_function;
            puzzle.attempts_count = requestJson.attempts_count;
            puzzle.jump_keyword = requestJson.jump_keyword;
            puzzle.extend_content = requestJson.extend_content;
            puzzle.extend_data = requestJson.extend_data;
            puzzle.analysis = requestJson.analysis;
            puzzle.dt_update = DateTime.Now;
            
            await puzzleDb.SimpleDb.AsUpdateable(puzzle).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/get-puzzle")]
        public async Task GetPuzzle(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var puzzleDb = DbFactory.Get<Puzzle>();
            var puzzleList = await puzzleDb.SimpleDb.AsQueryable().OrderBy(it => it.pgid).OrderBy(it => it.pid).WithCache().ToListAsync();

            await response.JsonResponse(200, new GetPuzzleResponse
            {
                status = 1,
                puzzle = puzzleList.ToList()
            });
        }

        [HttpHandler("POST", "/admin/get-additional-answer")]
        public async Task GetAdditionalAnswer(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<DeletePuzzleRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var aadb = DbFactory.Get<AdditionalAnswer>();
            var res = await aadb.SimpleDb.AsQueryable().Where(it => it.pid == requestJson.pid).ToListAsync();

            await response.JsonResponse(200, new GetAdditionalAnswerResponse
            {
                status = 1,
                additional_answer = res
            });
        }

        [HttpHandler("POST", "/admin/add-additional-answer")]
        public async Task AddAdditionalAnswer(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<additional_answer>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            requestJson.aaid = 0;

            var aadb = DbFactory.Get<AdditionalAnswer>();
            await aadb.SimpleDb.AsInsertable(requestJson).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/edit-additional-answer")]
        public async Task EditAdditionalAnswer(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<additional_answer>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var aadb = DbFactory.Get<AdditionalAnswer>();
            await aadb.SimpleDb.AsUpdateable(requestJson).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/delete-additional-answer")]
        public async Task DeleteAdditionalAnswer(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<DeleteAdditionalAnswerRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var aadb = DbFactory.Get<AdditionalAnswer>();
            await aadb.SimpleDb.AsDeleteable().Where(it => it.aaid == requestJson.aaid).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/get-tips")]
        public async Task GetTips(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<DeletePuzzleRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var tdb = DbFactory.Get<PuzzleTips>();
            var res = await tdb.SimpleDb.AsQueryable().Where(it => it.pid == requestJson.pid).OrderBy(it => it.order).ToListAsync();

            await response.JsonResponse(200, new GetTipsResponse
            {
                status = 1,
                tips = res
            });
        }

        [HttpHandler("POST", "/admin/add-tips")]
        public async Task AddTips(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<puzzle_tips>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            requestJson.ptid = 0;

            var tdb = DbFactory.Get<PuzzleTips>();
            await tdb.SimpleDb.AsInsertable(requestJson).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/edit-tips")]
        public async Task EditTips(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<puzzle_tips>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var tdb = DbFactory.Get<PuzzleTips>();
            await tdb.SimpleDb.AsUpdateable(requestJson).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/delete-tips")]
        public async Task DeleteTips(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<DeleteTipsRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var tdb = DbFactory.Get<PuzzleTips>();
            await tdb.SimpleDb.AsDeleteable().Where(it => it.ptid == requestJson.ptid).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/swap-pids")]
        public async Task SwapPids(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var requestJson = request.Json<SwapPidsRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            var pid1 = requestJson.pid1;
            var pid2 = requestJson.pid2;

            var pdb = DbFactory.Get<Puzzle>();
            var aadb = DbFactory.Get<AdditionalAnswer>();
            var tdb = DbFactory.Get<PuzzleTips>();

            //读出puzzle数据库中的两条数据
            var puzzle1 = await pdb.SimpleDb.AsQueryable().Where(it => it.pid == pid1).FirstAsync();
            var puzzle2 = await pdb.SimpleDb.AsQueryable().Where(it => it.pid == pid2).FirstAsync();

            puzzle1.pid = pid2;
            puzzle2.pid = pid1;

            //删除原有的pid1和pid2两条数据
            await pdb.SimpleDb.AsDeleteable().Where(it => it.pid == pid1 || it.pid == pid2).ExecuteCommandAsync();
            //插入新的两条数据
            await pdb.SimpleDb.AsInsertable(new List<puzzle> { puzzle1, puzzle2 }).RemoveDataCache().ExecuteCommandAsync();


            //交换附加数据库
            var tempPid = -pid1;

            //先将pid1批量替换为tempPid
            await aadb.SimpleDb.AsUpdateable().SetColumns(it => new additional_answer { pid = tempPid }).Where(it => it.pid == pid1).ExecuteCommandAsync();
            await tdb.SimpleDb.AsUpdateable().SetColumns(it => new puzzle_tips { pid = tempPid }).Where(it => it.pid == pid1).ExecuteCommandAsync();

            //再将pid2批量替换为pid1
            await aadb.SimpleDb.AsUpdateable().SetColumns(it => new additional_answer { pid = pid1 }).Where(it => it.pid == pid2).ExecuteCommandAsync();
            await tdb.SimpleDb.AsUpdateable().SetColumns(it => new puzzle_tips { pid = pid1 }).Where(it => it.pid == pid2).ExecuteCommandAsync();

            //再将tempPid批量替换为pid2
            await aadb.SimpleDb.AsUpdateable().SetColumns(it => new additional_answer { pid = pid2 }).Where(it => it.pid == tempPid).RemoveDataCache().ExecuteCommandAsync();
            await tdb.SimpleDb.AsUpdateable().SetColumns(it => new puzzle_tips { pid = pid2 }).Where(it => it.pid == tempPid).RemoveDataCache().ExecuteCommandAsync();

            await response.OK();
        }
    }
}
