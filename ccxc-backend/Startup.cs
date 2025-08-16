using Ccxc.Core.HttpServer.Middlewares;
using ccxc_backend.Functions.Plugins;
using Ccxc.Core.Utils;
using System.Threading;

namespace ccxc_backend
{
    internal class Startup
    {
        public AutoResetEvent StopSignal { get; set; } = new AutoResetEvent(false);
        public bool Running { get; private set; } = false;

        private Ccxc.Core.HttpServer.Server server;

        internal void Run()
        {
            Running = true;

            //初始化数据库
            Ccxc.Core.Utils.Logger.Info("正在初始化数据库。");

            var dm = new DataServices.DbMaintenance(Config.Config.Options.DbConnStr, Config.Config.Options.RedisConnStr);
            dm.InitDatabase();

            //初始化Redis连接池
            Ccxc.Core.Utils.Logger.Info("正在初始化Redis连接池。");
            RedisClient.InitializeConnectionPool(Config.Config.Options.RedisConnStr, Config.Config.Options.RedisConnectionPoolSize);

            //初始化配置
            Ccxc.Core.Utils.Logger.Info("正在加载配置。");
            Config.SystemConfigLoader.LoadConfig().GetAwaiter().GetResult();

            //注册HTTP控制器组件
            Controllers.ControllerRegister.Regist();

            //注册插件
            Ccxc.Core.Utils.Logger.Info("开始加载插件。");
            PluginLoader.Init();
            PluginLoader.LoadPlugins().GetAwaiter().GetResult();

            //启动HTTP服务
            Ccxc.Core.Utils.Logger.Info("正在启动HTTP服务。");
            server = Ccxc.Core.HttpServer.Server.GetServer(Config.Config.Options.HttpPort, "api/v1");
            server.DebugMode = Config.Config.Options.DebugMode;
            server.UseCors().Run();
        }

        internal void Wait()
        {
            StopSignal.WaitOne();
            while (Running)
            {
                StopSignal.WaitOne();
            }
        }

        internal async void StopServer()
        {
            Running = false;
            
            //停止HTTP服务
            await server.Stop();
            
            //释放Redis连接池资源
            Ccxc.Core.Utils.Logger.Info("正在释放Redis连接池资源。");
            RedisClient.DisposeConnectionPool();
            
            StopSignal.Set();
        }
    }
}
