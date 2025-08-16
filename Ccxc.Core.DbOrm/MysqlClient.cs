using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Ccxc.Core.DbOrm
{
    public abstract class MysqlClient<T> where T : class, new()
    {
        public SqlBaseClient Db
        {
            get
            {
                return new SqlBaseClient(ConnStr, CacheConfig, DbType.MySql, IfInitKeyFromAttribute);
            }
        }

        protected string ConnStr;
        protected bool IfInitKeyFromAttribute;
        protected RedisCacheConfig CacheConfig;

        public int TenantId { get; set; }

        public MysqlClient(string connStr, RedisCacheConfig cacheConfig)
        {
            ConnStr = connStr;
            IfInitKeyFromAttribute = true;
            CacheConfig = cacheConfig;
        }

        public virtual string GetDatabaseName() => ConnStr.Split(';')
                                                  .Select(cf => cf.Split('='))
                                                  .Where(cf => cf.Length >= 2 && cf[0].ToLower() == "database")
                                                  .Select(cf => cf[1].Trim())
                                                  .First() ?? "";

        public SimpleClient<T> SimpleDb => new SimpleClient<T>(Db);
        public virtual void InitTable() => Db.CodeFirst.InitTables<T>();
    }
}
