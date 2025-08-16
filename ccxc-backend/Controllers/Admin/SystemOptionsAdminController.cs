using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Ccxc.Core.HttpServer;
using ccxc_backend.Config;
using ccxc_backend.Controllers.System;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using Newtonsoft.Json;

namespace ccxc_backend.Controllers.Admin
{
    [Export(typeof(HttpController))]
    public class SystemOptionsAdminController : HttpController
    {
        [HttpHandler("POST", "/admin/get-system-options")]
        public async Task GetSystemOptions(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            await SystemConfigLoader.LoadConfig();

            var currentConfig = SystemConfigLoader.Config;

            // deepClone 一份当前配置，避免对原配置造成影响。
            var cloneConfig = JsonConvert.DeserializeObject<SystemConfig>(JsonConvert.SerializeObject(currentConfig));

            // 将 AdminAiApiKey 只保留前4个字符。后面用 * 代替。
            cloneConfig.AdminAiApiKey = cloneConfig.AdminAiApiKey[..4] + "********";

            await response.JsonResponse(200, new SystemOptionsAdminResponse
            {
                status = 1,
                config = cloneConfig
            });
        }

        [HttpHandler("POST", "/admin/update-system-options")]
        public async Task UpdateSystemOptions(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Administrator);
            if (userSession == null) return;

            var requestJson = request.Json<SystemConfig>();

            // 获取所有系统配置项
            var systemOptionsDb = DbFactory.Get<SystemOptions>();
            var allOptions = await systemOptionsDb.SimpleDb.AsQueryable().ToListAsync();
            var allOptionsDict = allOptions.ToDictionary(x => x.key, x => x);

            // 获取SystemConfig的所有属性
            var properties = typeof(SystemConfig).GetProperties();

            // 遍历每个属性，将值更新到数据库
            foreach (var property in properties)
            {
                string key = property.Name;
                var value = property.GetValue(requestJson)?.ToString();

                //判断 AdminAiApiKey ，如果用户没有提交，则不更新
                if (key == "AdminAiApiKey")
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        continue;
                    }
                }

                if (value != null)
                {
                    // 检查该配置项是否已存在
                    if (allOptionsDict.ContainsKey(key))
                    {
                        // 更新现有配置项
                        var option = allOptionsDict[key];
                        option.value = value;
                        await systemOptionsDb.SimpleDb.AsUpdateable(option).RemoveDataCache().ExecuteCommandAsync();
                    }
                    else
                    {
                        // 添加新配置项
                        var newOption = new system_options
                        {
                            key = key,
                            value = value
                        };
                        await systemOptionsDb.SimpleDb.AsInsertable(newOption).RemoveDataCache().ExecuteCommandAsync();
                    }
                }
            }

            // 重新加载配置
            await SystemConfigLoader.LoadConfig();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/get-cached-scoreboard-time")]
        public async Task GetCachedScoreboardTime(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            //使用缓存的排行榜数据
            var cachef = DbFactory.GetCache();
            var staticScoreboardKey = cachef.GetCacheKey("scoreboard_static_cache");
            var cacheData = await cachef.Get<ScoreBoardResponse>(staticScoreboardKey);
            if (cacheData != null)
            {
                //缓存有效，直接返回
                await response.JsonResponse(200, new StaticScoreboardTimeResponse
                {
                    status = 1,
                    data = cacheData.extra_data
                });
                return;
            }

            await response.JsonResponse(200, new StaticScoreboardTimeResponse
            {
                status = 1,
                data = "[当前没有缓存的静态排行榜数据]"
            });
        }

        [HttpHandler("POST", "/admin/set-cached-scoreboard-time")]
        public async Task SetCachedScoreboardTime(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Administrator);
            if (userSession == null) return;

            var now = DateTime.Now;
            var scoreboardData = await SystemController.GetScoreBoardData(now, true);
            scoreboardData.extra_data = $"静态排行榜数据生成时间：{now:yyyy-MM-dd HH:mm:ss}";


            //保存缓存的排行榜数据
            var cachef = DbFactory.GetCache();
            var staticScoreboardKey = cachef.GetCacheKey("scoreboard_static_cache");

            await cachef.Put(staticScoreboardKey, scoreboardData);

            await response.OK();
        }
    }
}