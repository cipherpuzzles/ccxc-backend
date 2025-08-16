using ccxc_backend.Config;
namespace ccxc_backend.Controllers.Admin
{
    public class SystemOptionsAdminResponse : BasicResponse
    {
        public SystemConfig config { get; set; }
    }

    public class StaticScoreboardTimeResponse : BasicResponse
    {
        public string data { get; set; }
    }
}
