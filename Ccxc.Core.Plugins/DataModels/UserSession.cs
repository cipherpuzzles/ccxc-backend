using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ccxc.Core.Plugins.DataModels
{
    public enum AuthLevel
    {
        Banned = -1,
        NeedActivate = 0,
        Normal = 1,
        Member = 2,
        TeamLeader = 3,
        Organizer = 4,
        Administrator = 5
    }

    public class UserSession
    {
        public int uid { get; set; } = 0;
        public string username { get; set; } = "";
        public int roleid { get; set; } = 0;
    }
}
