using Ccxc.Core.DbOrm;
using Ccxc.Core.HttpServer;
using Ccxc.Core.Utils.ExtensionFunctions;
using ccxc_backend.DataModels;
using ccxc_backend.Functions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ccxc_backend.Controllers.Users
{
    public class UserRegRequest
    {
        [Required(Message = "用户名不能为空")]
        public string username { get; set; }

        [Required(Message = "E-mail不能为空")]
        public string email { get; set; }

        [Required(Message = "密码不能为空")]
        public string pass { get; set; }

        public string code { get; set; }

        public string nonce { get; set; }
        public string userid { get; set; }
    }

    public class UserRegResponse : BasicResponse
    {
        public int is_send_email { get; set; }
    }

    public class CheckTicketRequest
    {
        [Required(Message = "Ticket Error")]
        public string ticket { get; set; }
    }

    public class UserLoginRequest
    {
        [Required(Message = "E-mail不能为空")]
        public string email { get; set; }

        [Required(Message = "密码不能为空")]
        public string pass { get; set; }
        public string code { get; set; }

        public string nonce { get; set; }

        public string userid { get; set; }
    }

    public class UserLoginResponse : BasicResponse
    {
        public UserLoginInfo user_login_info { get; set; }
        public class UserLoginInfo
        {
            public int uid { get; set; }
            public string username { get; set; }
            public int roleid { get; set; }
            public string token { get; set; }
            public string sk { get; set; }
            public string etc { get; set; }
            public string color { get; set; }
        }
    }

    public class PuzzleCheckTicketResponse : UserLoginResponse
    {
        /// <summary>
        /// 正式游戏服务中登陆后跳转的页面（0-序章 1-本篇）
        /// </summary>
        public int open_type { get; set; }
    }

    public class ModifyPasswordRequest
    {
        [Required(Message = "原密码不能为空")]
        public string old_pass { get; set; }

        [Required(Message = "新密码不能为空")]
        public string pass { get; set; }

        public string code { get; set; }
        public string nonce { get; set; }
        public string userid { get; set; }
    }

    public class EditUserRequest
    {
        [Required(Message = "用户名不能为空")]
        public string username { get; set; }
        public string phone { get; set; }
        public string profile { get; set; }
        public string theme_color { get; set; }
        public int gender { get; set; }
        public string third_pron { get; set; }
    }

    public class UserInfo
    {
        public int uid { get; set; }
        public string username { get; set; }
        public string email { get; set; }
        public string phone { get; set; }
        public int roleid { get; set; }
        public string theme_color { get; set; }
        public string avatar_hash { get; set; }
        public int gender { get; set; }
        public string third_pron { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        public DateTime create_time { get; set; }

        [JsonConverter(typeof(UnixTimestampConverter))]
        public DateTime update_time { get; set; }
        public string profile { get; set; }

        public UserInfo(user d)
        {
            uid = d.uid;
            username = d.username;
            email = d.email;
            phone = d.phone;
            roleid = d.roleid;
            create_time = d.create_time;
            update_time = d.update_time;
            profile = d.profile;
            theme_color = d.theme_color;
            gender = d.gender;
            third_pron = d.third_pron;
            avatar_hash = CryptoUtils.GetAvatarHash(d.email);
        }

        public UserInfo()
        {

        }
    }

    public class GroupInfo : user_group
    {
        public List<UserInfo> member_list { get; set; }

        public GroupInfo(user_group d)
        {
            gid = d.gid;
            groupname = d.groupname;
            create_time = d.create_time;
            update_time = d.update_time;
            profile = d.profile;
        }

        public GroupInfo()
        {

        }
    }

    public class MyProfileResponse : BasicResponse
    {
        public UserInfo user_info { get; set; }
        public GroupInfo group_info { get; set; }
    }

    public class SearchNoGroupUserRequest
    {
        public string kw_uname { get; set; }
    }

    public class SearchNoGroupUserResponse : BasicResponse
    {
        public List<UserSearchResult> result { get; set; }

        public class UserSearchResult
        {
            public int uid { get; set; }
            public string username { get; set; }
            public string avatar_hash { get; set; }
            public string theme_color { get; set; }
            public string profile { get; set; }

            public UserSearchResult() { }

            public UserSearchResult(user u)
            {
                uid = u.uid;
                username = u.username;
                theme_color = u.theme_color;
                profile = u.profile;
                avatar_hash = CryptoUtils.GetAvatarHash(u.email);
            }
        }
    }

    public class EmailResetPassRequest
    {
        [Required(Message = "E-mail不能为空")]
        public string email { get; set; }

        [Required(Message = "验证码不能为空")]
        public string code { get; set; }

        public string nonce { get; set; }

        public string userid { get; set; }
    }

    public class ResetPassCheckTokenRequest
    {
        [Required(Message = "Token不能为空")]
        public string token { get; set; }
    }

    public class ResetPassCheckTokenResponse : BasicResponse
    {
        public string email { get; set; }
    }

    public class ResetPasswordRequest
    {
        [Required(Message = "Token不能为空")]
        public string token { get; set; }

        [Required(Message = "新密码不能为空")]
        public string pass { get; set; }

        public string code { get; set; }
        public string nonce { get; set; }
        public string userid { get; set; }
    }

    public class SsoCheckRequest
    {
        [Required(Message = "token不能为空")]
        public string token { get; set; }

        [Required(Message = "callback_url不能为空")]
        public string callback_url { get; set; }
    }

    public class SsoLoginRequest
    {
        public string userid { get; set; }
    }

}
