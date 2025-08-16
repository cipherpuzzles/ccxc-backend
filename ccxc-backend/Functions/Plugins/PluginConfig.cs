using Ccxc.Core.Plugins;

namespace ccxc_backend.Functions.Plugins
{
    public class PluginConfig : IConfig
    {
        public string RedisConnStr { get => Config.Config.Options.RedisConnStr; }
        public string DbConnStr { get => Config.Config.Options.DbConnStr; }
        public string ImageStorage { get => Config.Config.Options.ImageStorage; }
        public string ImagePrefix
        {
            get => Config.Config.Options.ImagePrefix;
        }
    }
}