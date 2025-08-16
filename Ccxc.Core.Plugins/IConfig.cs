using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ccxc.Core.Plugins
{
    public interface IConfig
    {
        /// <summary>
        /// Redis连接字符串
        /// </summary>
        public string RedisConnStr { get; }

        /// <summary>
        /// 数据库连接字符串
        /// </summary>
        public string DbConnStr { get; }

        /// <summary>
        /// 图片存储路径
        /// </summary>
        public string ImageStorage { get; }

        /// <summary>
        /// 图片引用前缀
        /// </summary>
        public string ImagePrefix { get; }
    }
}
