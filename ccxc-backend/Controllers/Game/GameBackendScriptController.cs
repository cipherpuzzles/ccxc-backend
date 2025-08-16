using Ccxc.Core.HttpServer;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using ccxc_backend.Functions;
using ccxc_backend.Functions.JavaScriptEngine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Game
{
    [Export(typeof(HttpController))]
    public class GameBackendScriptController : HttpController
    {
        [HttpHandler("POST", "/play/call-puzzle-backend")]
        public async Task CallPuzzleBackend(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Member, true);
            if (userSession == null) return;

            var requestJson = request.Json<CallPuzzleScriptRequest>();

            //判断请求是否有效
            if (!Validation.Valid(requestJson, out string reason))
            {
                await response.BadRequest(reason);
                return;
            }

            //取得该用户GID
            var groupBindItem = await ResponseExtend.GetUserGroupBind(userSession);
            if (groupBindItem == null)
            {
                await response.BadRequest("用户所属队伍不存在。");
                return;
            }
            //组队
            var gid = groupBindItem.gid;

            //取得进度
            var progressDb = DbFactory.Get<Progress>();
            var progress = await progressDb.SimpleDb.AsQueryable().Where(it => it.gid == gid).FirstAsync();
            if (progress == null)
            {
                await response.BadRequest("没有进度，请返回首页重新开始。");
                return;
            }

            var progressData = progress.data;
            if (progressData == null)
            {
                await response.BadRequest("未找到可用存档，请联系管理员。");
                return;
            }

            //取出key对应的脚本
            var scriptDb = DbFactory.Get<PuzzleBackendScript>();
            var scriptItem = await scriptDb.SimpleDb.AsQueryable().Where(it => it.key == requestJson.key).WithCache().FirstAsync();
            if (scriptItem == null)
            {
                await response.BadRequest("未找到对应脚本。");
                return;
            }

            //取出stores并解码
            var store = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(requestJson.stores))
            {
                var nonce = requestJson.nonce;
                if (string.IsNullOrEmpty(nonce))
                {
                    await response.BadRequest("stores非空时nonce不能为空。");
                    return;
                }

                var encryptedString = requestJson.stores;
                var decryptedString = CryptoUtils.AESDecrypt(encryptedString, nonce);

                store = JsonConvert.DeserializeObject<Dictionary<string, string>>(decryptedString);
            }

            //准备注入JS引擎的环境
            var requestData = requestJson.data;
            var script = scriptItem.script;
            var jsEngine = new Jurassic.ScriptEngine();
            var puzzleContext = new PuzzleScriptContext(jsEngine, store, requestData, userSession, gid, progress.data);
            jsEngine.SetGlobalValue("ctx", puzzleContext);
            jsEngine.Execute(script);

            //如果problemStatus改变了，则需要回写
            if (puzzleContext.isStatusChanged)
            {
                await progressDb.SimpleDb.AsUpdateable(progress).UpdateColumns(it => new { it.data }).ExecuteCommandAsync();
            }

            //如果有标记题目完成的函数被调用，则执行
            if (puzzleContext.callMakePuzzleFinishedRequest?.isCalled == true)
            {
                await JSEFunctions.MakePuzzleFinished(puzzleContext.callMakePuzzleFinishedRequest.gid,
                    puzzleContext.callMakePuzzleFinishedRequest.pid, 
                    puzzleContext.callMakePuzzleFinishedRequest.uid,
                    puzzleContext.callMakePuzzleFinishedRequest.username, 
                    puzzleContext.callMakePuzzleFinishedRequest.message);
            }

            //重新加密stores
            var newIV = CryptoUtils.GenRandomIV();
            var newStores = JsonConvert.SerializeObject(puzzleContext.store);
            var newEncryptedStores = CryptoUtils.AESEncrypt(newStores, newIV);


            var res = new CallPuzzleScriptResponse
            {
                status = 1,
                data = puzzleContext.response,
                stores = newEncryptedStores,
                nonce = newIV
            };
            await response.JsonResponse(200, res);
        }
    }

    public class CallPuzzleScriptRequest
    {
        public string key { get; set; }
        public string data { get; set; }
        public string stores { get; set; }
        public string nonce { get; set; }
    }

    public class CallPuzzleScriptResponse : BasicResponse
    {
        public string data { get; set; }
        public string stores { get; set; }
        public string nonce { get; set; }
    }
}
