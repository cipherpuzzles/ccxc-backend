using Ccxc.Core.DbOrm;
using ccxc_backend.DataModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace ccxc_backend.DataServices
{
    public class DbMaintenance
    {
        public SqlBaseClient DbBase;
        public RedisCacheConfig RedisConfig;
        public string DbConnStr;
        public string RedisConnStr;

        public DbMaintenance(string connStr, string redisConnStr)
        {
            RedisConnStr = redisConnStr;
            RedisConfig = new RedisCacheConfig()
            {
                RedisConnStr = redisConnStr,
                RedisDatabase = 0
            };

            DbConnStr = connStr;
            DbBase = new SqlBaseClient(connStr, RedisConfig, SqlSugar.DbType.MySql);
        }

        public void InitDatabase()
        {
            DbBase.DbMaintenance.CreateDatabase();

            new AdditionalAnswer(DbConnStr, RedisConfig).InitTable();
            new Announcement(DbConnStr, RedisConfig).InitTable();
            new AnswerLog(DbConnStr, RedisConfig).InitTable();
            new Article(DbConnStr, RedisConfig).InitTable();
            new Invite(DbConnStr, RedisConfig).InitTable();
            new LoginLog(DbConnStr, RedisConfig).InitTable();
            new Message(DbConnStr, RedisConfig).InitTable();
            new MyophLog(DbConnStr, RedisConfig).InitTable();
            new DataModels.Oracle(DbConnStr, RedisConfig).InitTable();
            new Plugin(DbConnStr, RedisConfig).InitTable();
            new Progress(DbConnStr, RedisConfig).InitTable();
            new Puzzle(DbConnStr, RedisConfig).InitTable();
            new PuzzleArticle(DbConnStr, RedisConfig).InitTable();
            new PuzzleBackendScript(DbConnStr, RedisConfig).InitTable();
            new PuzzleGroup(DbConnStr, RedisConfig).InitTable();
            new PuzzleTips(DbConnStr, RedisConfig).InitTable();
            new PuzzleVote(DbConnStr, RedisConfig).InitTable();
            new SystemOptions(DbConnStr, RedisConfig).InitTable();
            new TempAnno(DbConnStr, RedisConfig).InitTable();
            new User(DbConnStr, RedisConfig).InitTable();
            new UserGroup(DbConnStr, RedisConfig).InitTable();
            new UserGroupBind(DbConnStr, RedisConfig).InitTable();
        }
    }
}
