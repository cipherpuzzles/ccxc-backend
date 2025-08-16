using Ccxc.Core.DbOrm;
using Ccxc.Core.Utils.ExtensionFunctions;
using Newtonsoft.Json;
using System;

namespace ccxc_backend.DataModels
{
    public class user
    {
        [DbColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDescription = "用户ID")]
        public int uid { get; set; }

        [DbColumn(ColumnDescription = "用户名", IndexGroupNameList = new string[] { "index_name" })]
        public string username { get; set; }

        [DbColumn(ColumnDescription = "E-mail")]
        public string email { get; set; }

        [DbColumn(ColumnDescription = "Salted HashKey")]
        public string hashkey { get; set; }

        [DbColumn(ColumnDescription = "手机号", IsNullable = true)]
        public string phone { get; set; }

        [DbColumn(ColumnDescription = "密码")]
        public string password { get; set; }

        /// <summary>
        /// 角色（-1-被封禁 0-未激活 1-标准用户 2-组员 3-组长 4-出题组 5-管理员）
        /// </summary>
        [DbColumn(DefaultValue = "0")]
        public int roleid { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "更新时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime update_time { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        [DbColumn(ColumnDescription = "创建时间", ColumnDataType = "DATETIME", Length = 6, DefaultValue = "0000-00-00 00:00:00.000000")]
        public DateTime create_time { get; set; }

        [DbColumn(ColumnDescription = "个人简介", ColumnDataType = "TEXT", IsNullable = true)]
        public string profile { get; set; }

        [DbColumn(ColumnDescription = "信息Key", IsNullable = true)]
        public string info_key { get; set; }

        [DbColumn(ColumnDescription = "主题色标", IsNullable = true)]
        public string theme_color { get; set; }

        [DbColumn(ColumnDescription = "性别（0-未知 1-男 2-女 3-中性 4-其他）", IsNullable = true)]
        public int gender { get; set; }

        [DbColumn(ColumnDescription = "第三人称代词", IsNullable = true)]
        public string third_pron { get; set; }
    }

    public class User : MysqlClient<user>
    {
        public User(string connStr, RedisCacheConfig rcc) : base(connStr, rcc)
        {

        }
    }

    public class UserSession
    {
        public int uid { get; set; }
        public int gid { get; set; }
        public string username { get; set; }
        public int roleid { get; set; }
        public string color { get; set; }
        public string third_pron { get; set; }

        /// <summary>
        /// User-Token
        /// </summary>
        public string token { get; set; }

        /// <summary>
        /// 秘密认证Key
        /// </summary>
        public string sk { get; set; }

        /// <summary>
        /// 登录时间
        /// </summary>
        [JsonConverter(typeof(UnixTimestampConverter))]
        public DateTime login_time { get; set; }

        /// <summary>
        /// 上次活动时间
        /// </summary>
        [JsonConverter(typeof(UnixTimestampConverter))]
        public DateTime last_update { get; set; }

        /// <summary>
        /// 本Session有效性 1-有效 0-无效（视为没有登录）
        /// </summary>
        public int is_active { get; set; }

        /// <summary>
        /// 当Session无效时返回给前端的消息
        /// </summary>
        public string inactive_message { get; set; }

        /// <summary>
        /// 是否为内测用户 0-普通用户 1-内测用户（内测用户无视开赛时间设置，可在任何时间范围内提交）
        /// </summary>
        public int is_betaUser { get; set; }

        public static UserSession Guest
        {
            get
            {
                return new UserSession
                {
                    uid = -1,
                    gid = -1,
                    username = "Guest",
                    roleid = 2,
                    color = Controllers.Users.UserController.GetRandomThemeColor(),
                    token = "",
                    sk = "",
                    login_time = DateTime.Now,
                    last_update = DateTime.Now,
                    is_active = 1,
                    inactive_message = "",
                    is_betaUser = 0
                };
            }
        }

        public static string GetThirdPron(int gender, string thirdPron)
        {
            if (gender == 0 || gender == 1) return "他";
            else if (gender == 2) return "她";
            else if (gender == 3) return "TA";
            else if (string.IsNullOrEmpty(thirdPron))
            {
                return "TA";
            }
            else
            {
                return thirdPron;
            }
        }
    }

    public class PuzzleLoginTicketSession
    {
        public string token { get; set; }
    }
}
