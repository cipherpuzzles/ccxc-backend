using Ccxc.Core.HttpServer;
using Ccxc.Core.Plugins.DataModels;
using ccxc_backend.DataModels;
using ccxc_backend.DataServices;
using System.Threading.Tasks;

namespace ccxc_backend.Controllers
{
    public class BasicResponse
    {
        /// <summary>
        /// 0-保留 1-成功 2-失败，message为错误提示 3-失败，并跳转location指定URL 4-失败并立即注销 13-成功并立即注销 31-提示前端当前用户需要激活
        /// </summary>
        public int status { get; set; }
        public string message { get; set; }
        public string location { get; set; }
    }

    public static class ResponseExtend
    {
        public static Task OK(this Response response)
        {
            return response.JsonResponse(200, new BasicResponse
            {
                status = 1
            });
        }

        public static Task BadRequest(this Response response, string message)
        {
            return response.JsonResponse(400, new BasicResponse
            {
                status = 2,
                message = message
            });
        }

        public static Task Forbidden(this Response response, string message)
        {
            return response.JsonResponse(403, new BasicResponse
            {
                status = 2,
                message = message
            });
        }

        public static Task Unauthorized(this Response response, string message)
        {
            return response.JsonResponse(401, new BasicResponse
            {
                status = 4,
                message = message
            });
        }

        public static Task InternalServerError(this Response response, string message)
        {
            return response.JsonResponse(500, new BasicResponse
            {
                status = 2,
                message = message
            });
        }

        public static async Task<user_group_bind> GetUserGroupBind(DataModels.UserSession userSession)
        {
            var groupBindDb = DbFactory.Get<UserGroupBind>();
            var groupBindItem = await groupBindDb.SimpleDb.AsQueryable().Where(x => x.uid == userSession.uid).FirstAsync();

            if (groupBindItem == null)
            {
                if (userSession.roleid == 4)
                {
                    groupBindItem = new user_group_bind
                    {
                        uid = userSession.uid,
                        gid = userSession.gid,
                        is_leader = 0
                    };
                }
                else
                {
                    return null;
                }
            }

            return groupBindItem;
        }
    }
}
