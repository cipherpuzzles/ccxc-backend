using Ccxc.Core.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace ccxc_backend.Config
{
    public class Config
    {
        [OptionDescription("HTTP服务端口")]
        public int HttpPort { get; set; } = 52412; //0xCCBC

        [OptionDescription("Redis服务器连接字符串")]
        public string RedisConnStr { get; set; } = "127.0.0.1:6379";

        [OptionDescription("Redis连接池大小，用于高并发场景下提升性能。默认：20")]
        public int RedisConnectionPoolSize { get; set; } = 20;

        [OptionDescription("数据库连接字符串，默认：Server=localhost;User=root;Database=ccxc_db;Port=3306;Password=lp1234xy;Charset=utf8mb4;ConvertZeroDateTime=True")]
        public string DbConnStr { get; set; } = "Server=localhost;User=root;Database=ccxc_db;Port=3306;Password=lp1234xy;Charset=utf8mb4;ConvertZeroDateTime=True";

        [OptionDescription("调试模式：调试模式打开时，捕获的异常详情将通过HTTP直接返回给客户端，关闭时只返回简单错误消息和500提示码。True-打开 False-关闭，默认为False")]
        public bool DebugMode { get; set; } = false;

        [OptionDescription("插件目录，插件将会存放在这里。默认：Plugins")]
        public string PluginDir { get; set; } = "Plugins";

        [OptionDescription("图片存储目录，上传的图片将会存放在这里。请注意：必须使用绝对路径！")]
        public string ImageStorage { get; set; } = "/var/www/static.ccxc.ikp.yt/static/images/";

        [OptionDescription("图片访问前缀。默认：https://static.ccxc.ikp.yt/static/images/")]
        public string ImagePrefix { get; set; } = "https://static.ccxc.ikp.yt/static/images/";

        [OptionDescription("密码Hash种子1，请自由设置，设置后不要修改")]
        public string PassHashKey1 { get; set; } = "1q2^xBoacW@w0ei_E#Y)";

        [OptionDescription("密码Hash种子2，请自由设置，设置后不要修改")]
        public string PassHashKey2 { get; set; } = "Kgv8i:%=by_Tpq?Azr-K";

        [OptionDescription("AES Master Key，请自由设置，切记不要泄露")]
        public string AESMasterKey { get; set; } = "Change Content!!!";

        [OptionDescription("阿里云邮件推送服务Access Key")]
        public string AliyunDmAccessKey { get; set; } = "";

        [OptionDescription("阿里云邮件推送服务Access Secret")]
        public string AliyunDmAccessSecret { get; set; } = "";

        [OptionDescription("第三方API认证Secret")]
        public string ThirdApiSecret { get; set; } = "Change Content!!!";

        [OptionDescription("填写 Prometheus API地址用于展示性能监控。不填时为不启用。请确保Prometheus已正确安装。例子：http://localhost:9090")]
        public string PrometheusApi { get; set; } = "";

        public static Config Options { get; set; } = SystemOption.GetOption<Config>("Config/CcxcConfig.xml");
    }
}
