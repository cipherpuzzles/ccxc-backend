using Ccxc.Core.HttpServer;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using ccxc_backend.Functions.Plugins;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers.Admin
{
    [Export(typeof(HttpController))]
    public class PluginAdminController : HttpController
    {
        [HttpHandler("POST", "/admin/list-plugins")]
        public async Task PluginList(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;


            var pluginDb = DbFactory.Get<Plugin>();
            var plugins = await pluginDb.SimpleDb.AsQueryable().ToListAsync();
            await response.JsonResponse(200, new
            {
                status = 1,
                plugin_list = plugins
            });
        }

        [HttpHandler("POST", "/admin/reload-plugin-info")]
        public async Task ReloadPluginInfo(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Administrator);
            if (userSession == null) return;

            try
            {
                await PluginLoader.ReloadPluginsFromDir();
                await response.OK();
            }
            catch (Exception ex)
            {
                await response.BadRequest($"重载插件信息失败: {ex.Message}");
            }
        }

        [HttpHandler("POST", "/admin/set-plugin-status")]
        public async Task SetPluginStatus(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Administrator);
            if (userSession == null) return;

            var requestJson = request.Json<SetPluginStatusRequest>();

            var pluginDb = DbFactory.Get<Plugin>();
            var plugin = await pluginDb.SimpleDb.AsQueryable().Where(x => x.plugin_name == requestJson.plugin_name).FirstAsync();
            if (plugin == null)
            {
                await response.BadRequest("插件不存在。");
                return;
            }

            // 更新插件状态
            plugin.status = requestJson.status;
            if (requestJson.status == 1)
            {
                plugin.active_time = DateTime.Now;
            }

            await pluginDb.SimpleDb.AsUpdateable(plugin).UpdateColumns(x => new { x.active_time, x.status }).ExecuteCommandAsync();

            await response.OK();
        }

        [HttpHandler("POST", "/admin/get-frontend-plugins")]
        public async Task GetFrontendPlugins(Request request, Response response)
        {
            var userSession = await CheckAuth.Check(request, response, AuthLevel.Organizer);
            if (userSession == null) return;

            var pluginDb = DbFactory.Get<Plugin>();
            var pluginList = await pluginDb.SimpleDb.AsQueryable().Where(x => x.status == 1).ToListAsync();

            var result = new List<FrontendPluginComponent>();

            foreach (var plugin in pluginList)
            {
                var pluginName = plugin.plugin_name;
                if (!string.IsNullOrEmpty(plugin.frontend_components))
                {
                    var pluginComponents = JsonConvert.DeserializeObject<List<FrontendPluginComponent>>(plugin.frontend_components);
                    if (pluginComponents?.Count > 0)
                    {
                        foreach (var component in pluginComponents)
                        {
                            component.plugin_name = pluginName;
                            result.Add(component);
                        }
                    }
                }
            }

            await response.JsonResponse(200, new GetFrontendPluginsResponse
            {
                status = 1,
                data = result
            });
        }
    }

    public class SetPluginStatusRequest
    {
        public string plugin_name { get; set; }
        public int status { get; set; } // 0-禁用 1-启用
    }

    public class GetFrontendPluginsResponse : BasicResponse
    {
        public List<FrontendPluginComponent> data { get; set; }
    }

    public class FrontendPluginComponent : FrontendComponentItem
    {
        public string plugin_name { get; set; }
    }
}
