using Ccxc.Core.HttpServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ccxc.Core.Plugins
{
    public interface IPlugin
    {
        /// <summary>
        /// 插件名称（插件唯一标识）
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 初始化
        /// </summary>
        Task Initialize();

        /// <summary>
        /// 注册HTTP 控制器到系统中
        /// </summary>
        /// <returns>需要注册的HTTP控制器的List</returns>
        Task<IEnumerable<HttpController>> RegisterControllers();
    }
}
