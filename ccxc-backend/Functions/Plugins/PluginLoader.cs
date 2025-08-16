using Newtonsoft.Json;
using System;
using System.IO;
using ccxc_backend.DataModels;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ccxc.Core.Utils;
using System.Reflection;
using Ccxc.Core.Plugins;
using Ccxc.Core.HttpServer;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace ccxc_backend.Functions.Plugins
{
    public static class PluginLoader
    {
        public static ServiceCollection services { get; set; } = new();
        public static ServiceProvider provider { get; set; } = null;

        public static void Init()
        {
            services.AddSingleton<IPluginAPI, PluginAPI>();
            services.AddSingleton<IConfig, PluginConfig>();
            provider = services.BuildServiceProvider();
        }


        public static async Task ReloadPluginsFromDir()
        {
            var pluginDir = Config.Config.Options.PluginDir;
            if (!Directory.Exists(pluginDir))
            {
                Directory.CreateDirectory(pluginDir);
            }

            // 读取插件目录下的所有子目录，每个子目录中应包含一个 menifest.json 文件，用于描述插件信息。
            var pluginDirs = Directory.GetDirectories(pluginDir);
            List<(string path, PluginManifest manifest)> manifests = new List<(string path, PluginManifest manifest)>();

            foreach (var dir in pluginDirs)
            {
                var manifestPath = Path.Combine(dir, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        // 读取 manifest.json 文件，获取插件信息。
                        var manifestJson = File.ReadAllText(manifestPath);
                        var manifest = JsonConvert.DeserializeObject<PluginManifest>(manifestJson);

                        if (manifest != null && !string.IsNullOrEmpty(manifest.name))
                        {
                            manifests.Add((dir, manifest));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"读取插件 {dir} 的manifest.json文件失败: {ex.Message}");
                    }
                }
            }


            // 获取插件数据库操作实例
            var pluginDb = DataServices.DbFactory.Get<Plugin>();
            var oldPluginList = await pluginDb.SimpleDb.AsQueryable().ToListAsync();

            // 检查是否有插件需要删除（即在数据库中存在但在目录中不存在）
            var oldPluginNames = new HashSet<string>(oldPluginList.Select(p => p.plugin_name));
            var needDeletePlugins = oldPluginList
                .Where(p => !manifests.Any(m => m.manifest.name == p.plugin_name))
                .Select(p => p.plugin_name)
                .ToList();
            if (needDeletePlugins.Count > 0)
            {
                await pluginDb.SimpleDb.AsDeleteable()
                    .Where(p => needDeletePlugins.Contains(p.plugin_name))
                    .ExecuteCommandAsync();
            }

            // 添加或更新插件信息到数据库
            var pluginList = new List<plugin>();

            foreach (var (path, manifest) in manifests)
            {
                var p = new plugin
                {
                    plugin_name = manifest.name,
                    plugin_title = manifest.title,
                    description = manifest.description,
                    version = manifest.version,
                    author = manifest.author,
                    entry_assembly = manifest.entry_assembly,
                    entry = manifest.entry,
                    icon = manifest.icon,
                    install_path = path,
                    frontend_components = manifest.frontendComponents != null
                        ? JsonConvert.SerializeObject(manifest.frontendComponents)
                        : null,
                };
                pluginList.Add(p);
            }


            try
            {
                var dbStorage = pluginDb.Db.Storageable(pluginList).ToStorage();
                await dbStorage.AsInsertable.ExecuteCommandAsync();
                await dbStorage.AsUpdateable.IgnoreColumns(x => new { x.status, x.active_time }).ExecuteCommandAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新插件列表数据失败: {ex.Message}");
            }
        }

        public static async Task LoadPlugins()
        {
            // 从数据库中读取已激活的插件列表
            var pluginDb = DataServices.DbFactory.Get<Plugin>();
            var activePlugins = await pluginDb.SimpleDb.AsQueryable().Where(x => x.status == 1).ToListAsync();

            foreach (var plugin in activePlugins)
            {
                Logger.Info($"加载插件: {plugin.plugin_name}");

                var path = plugin.install_path;
                var dllName = plugin.entry_assembly;
                var assemblyPath = Path.Combine(path, dllName);
                if (!File.Exists(assemblyPath))
                {
                    Logger.Error($"插件 {plugin.plugin_name} 的入口程序集不存在: {assemblyPath}");
                    continue;
                }

                try
                {
                    // 将assemblyPath解析为绝对路径
                    assemblyPath = Path.GetFullPath(assemblyPath);

                    // 加载插件的dll
                    var assembly = Assembly.LoadFile(assemblyPath);
                    var pluginType = assembly.GetType(plugin.entry, throwOnError: true);
                    if (pluginType == null)
                    {
                        Logger.Error($"插件 {plugin.plugin_name} 的入口类型不存在: {plugin.entry}");
                        continue;
                    }

                    // 插件入口类型必须实现 IPlugin 接口
                    if (!typeof(IPlugin).IsAssignableFrom(pluginType))
                    {
                        Logger.Error($"插件 {plugin.plugin_name} 的入口类型必须实现 IPlugin 接口: {plugin.entry}");
                        continue;
                    }

                    // 创建实例，完成依赖注入
                    var pluginObject = (IPlugin)ActivatorUtilities.CreateInstance(provider, pluginType);

                    // 初始化插件
                    await pluginObject.Initialize();

                    // 获取插件中的Controllers
                    var controllers = await pluginObject.RegisterControllers();
                    foreach (var controller in controllers)
                    {
                        Server.RegisterController(controller);
                    }

                    Logger.Info($"插件 {plugin.plugin_name} 加载成功。");
                }
                catch (Exception ex)
                {
                    Logger.Error($"加载插件 {plugin.plugin_name} 失败: {ex.Message}");
                }
            }

        }
    }

    public class PluginManifest
    {
        public string name { get; set; }

        public string title { get; set; }

        public string description { get; set; }

        public string version { get; set; }

        public string author { get; set; }

        public string entry_assembly { get; set; }

        public string entry { get; set; }

        public string icon { get; set; }

        public List<FrontendComponentItem> frontendComponents { get; set; }
    }

    public class FrontendComponentItem
    {
        public string name { get; set; }
        public string path { get; set; }
        public string component { get; set; }
        public string icon { get; set; }
    }
}