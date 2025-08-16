using SqlSugar;
using System.Linq;

namespace Ccxc.Core.DbOrm
{
    public class SqlBaseClient : SqlSugarClient
    {
        public SqlBaseClient(string connStr, RedisCacheConfig cacheConfig, DbType dbtype) : base(new ConnectionConfig
        {
            DbType = dbtype,
            ConnectionString = connStr,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                DataInfoCacheService = new RedisCache(cacheConfig)
            }
        })
        {
            ConnStr = connStr;
        }

        public SqlBaseClient(string connStr, RedisCacheConfig cacheConfig, DbType dbtype, bool initKeyFromAttribute) : base(new ConnectionConfig
        {
            DbType = dbtype,
            ConnectionString = connStr,
            IsAutoCloseConnection = true,
            InitKeyType = initKeyFromAttribute ? InitKeyType.Attribute : InitKeyType.SystemTable,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                DataInfoCacheService = new RedisCache(cacheConfig)
            }
        })
        {
            ConnStr = connStr;
        }

        public SqlBaseClient(ConnectionConfig connConfig) : base(connConfig)
        {
            ConnStr = connConfig.ConnectionString;
        }

        public string ConnStr { get; set; }
        public virtual string GetDatabaseName() => ConnStr.Split(';')
                                                  .Select(cf => cf.Split('='))
                                                  .Where(cf => cf.Length >= 2 && cf[0].ToLower() == "database")
                                                  .Select(cf => cf[1].Trim())
                                                  .First() ?? "";
    }
}
