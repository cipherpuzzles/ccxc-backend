using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using System.Reflection;

namespace ccxc_backend.Config
{
    public static class SystemConfigLoader
    {
        public static SystemConfig Config { get; set; }

        public static async Task LoadConfig()
        {
            Config = await GetOption<SystemConfig>();
        }

        private static async Task<T> GetOption<T>() where T : class, new()
        {
            var systemOptionsDb = DbFactory.Get<SystemOptions>();
            var allSystemOptionList = await systemOptionsDb.SimpleDb.AsQueryable().ToListAsync();
            var allSystemOptionDict = allSystemOptionList.ToDictionary(x => x.key, x => x.value);

            var config = new T();
            
            // 获取T类型的所有属性
            PropertyInfo[] properties = typeof(T).GetProperties();
            
            foreach (var property in properties)
            {
                // 属性名作为配置的key
                string key = property.Name;
                
                // 如果数据库中存在该key
                if (allSystemOptionDict.ContainsKey(key))
                {
                    string value = allSystemOptionDict[key];
                    
                    try
                    {
                        // 根据属性类型将字符串值转换为对应类型
                        if (property.PropertyType == typeof(string))
                        {
                            property.SetValue(config, value);
                        }
                        else if (property.PropertyType == typeof(int))
                        {
                            property.SetValue(config, int.Parse(value));
                        }
                        else if (property.PropertyType == typeof(long))
                        {
                            property.SetValue(config, long.Parse(value));
                        }
                        else if (property.PropertyType == typeof(bool))
                        {
                            // 支持多种布尔值表示方式
                            bool boolValue = false;
                            if (bool.TryParse(value, out boolValue))
                            {
                                property.SetValue(config, boolValue);
                            }
                            else if (value == "1" || value.ToLower() == "yes" || value.ToLower() == "true")
                            {
                                property.SetValue(config, true);
                            }
                            else if (value == "0" || value.ToLower() == "no" || value.ToLower() == "false")
                            {
                                property.SetValue(config, false);
                            }
                        }
                        else if (property.PropertyType == typeof(DateTime))
                        {
                            property.SetValue(config, DateTime.Parse(value));
                        }
                    }
                    catch (Exception ex)
                    {
                        // 转换失败时记录错误并使用默认值
                        Console.WriteLine($"Error parsing config value for {key}: {ex.Message}");
                    }
                }
                // 如果数据库中不存在该key，则使用默认值（T的构造函数中设置的值）
            }
            
            return config;
        }
    }

    public class SystemConfig
    {
        /// <summary>
        /// 项目名称
        /// </summary>
        public string ProjectName { get; set; } = "ACPH: A CCXC Puzzle Hunt";

        /// <summary>
        /// 项目前端地址前缀
        /// </summary>
        public string ProjectFrontendPrefix { get; set; } = "https://www.ccxc.ikp.yt:13880";

        /// <summary>
        /// 组队人数上限，默认5人
        /// </summary>
        public int MaxGroupSize { get; set; } = 5;

        /// <summary>
        /// 用户Session有效期，单位秒
        /// </summary>
        public int UserSessionTimeout { get; set; } = 604800;

        /// <summary>
        /// 开赛时间，Unix时间戳（毫秒）
        /// </summary>
        public long StartTime { get; set; } = 1691755200000;

        /// <summary>
        /// 完赛时间，Unix时间戳（毫秒）
        /// </summary>
        public long EndTime { get; set; } = 1692705600000;

        /// <summary>
        /// 题目部分独立前端地址前缀（仅域名，不要以/结尾）
        /// </summary>
        public string GamePrefix { get; set; } = "https://puzzle.ccxc.ikp.yt:20480";

        /// <summary>
        /// 题目部分Websocket地址前缀（不要以 / 结尾）
        /// </summary>
        public string WebsocketPrefix { get; set; } = "wss://api.ccxc.ikp.yt:22443/ws-api";

        /// <summary>
        /// 是否显示题目解析，0-不显示 1-显示，默认0
        /// </summary>
        public int ShowAnalysis { get; set; } = 0;

        /// <summary>
        /// 是否打开访客模式，打开后可在不登录状态下看题。0-关闭 1-打开。默认0
        /// </summary>
        public int EnableGuestMode { get; set; } = 0;

        /// <summary>
        /// 使用缓存中的固定排行榜数据。0-不使用 1-使用。默认0
        /// </summary>
        public int UseCachedScoreboard { get; set; } = 0;

        /// <summary>
        /// 是否启用管理员后台AI自动补全，0-关闭 1-打开。默认0
        /// </summary>
        public int AdminAiEnable { get; set; } = 0;

        /// <summary>
        /// 管理员后台AI自动补全：调用的API地址。填写以 v1结尾，如 https://api.openai.com/v1
        /// </summary>
        public string AdminAiApiUrl { get; set; } = "https://api.openai.com/v1";

        /// <summary>
        /// 管理员后台AI自动补全：调用的API密钥
        /// </summary>
        public string AdminAiApiKey { get; set; } = "sk-proj-1234567890";

        /// <summary>
        /// 管理员后台AI自动补全：调用的API模型
        /// </summary>
        public string AdminAiApiModel { get; set; } = "gpt-4o";
    }
}
