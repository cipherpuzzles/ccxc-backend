using Ccxc.Core.HttpServer;
using Ccxc.Core.Plugins.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ccxc.Core.Plugins
{
    public interface IPluginAPI
    {
        /// <summary>
        /// 检查用户的登录状态。如果用户已登录，返回用户Session，否则返回null。
        /// 如果这个接口返回了null，那么接下来你应该直接return;
        /// </summary>
        /// <param name="request">HTTP请求</param>
        /// <param name="response">HTTP响应</param>
        /// <param name="authLevel">此接口最低允许使用的权限</param>
        /// <param name="onlyInGaming">是否仅允许比赛中调用</param>
        /// <returns>返回用户Session</returns>
        Task<UserSession> CheckAuth(Request request, Response response, AuthLevel authLevel, bool onlyInGaming = false);

        /// <summary>
        /// 获取当前用户组队和题目存档状态。注意：未开赛前只会返回gid。
        /// </summary>
        /// <param name="uid">用户UID</param>
        /// <returns>题目存档状态</returns>
        Task<ProgressData> GetProgressData(int uid);

        /// <summary>
        /// 保存题目额外状态
        /// </summary>
        /// <param name="gid">队伍GID</param>
        /// <param name="pid">题目PID</param>
        /// <param name="key">状态信息Key</param>
        /// <param name="value">状态信息值</param>
        /// <returns></returns>
        Task SavePuzzleProgress(int gid, int pid, string key, string value);

        /// <summary>
        /// 消耗信用点。从队伍中扣除指定的信用点（cost为负数时为增加信用点）。
        /// </summary>
        /// <param name="gid">队伍GID</param>
        /// <param name="cost">信用点数量</param>
        /// <returns>如果操作成功，返回true。否则返回false。返回false一般为用户信用点余额不足。</returns>
        Task<bool> CostCredit(int gid, int cost);

        /// <summary>
        /// 添加题目提交日志
        /// </summary>
        /// <param name="uid">用户UID</param>
        /// <param name="gid">队伍GID</param>
        /// <param name="pid">题目PID</param>
        /// <param name="answer">答案</param>
        /// <param name="status">判题结果（1-正确 2-答案错误 3-答题次数用尽 4-里程碑 5-发生存档错误而未判定 6-该题目不可见而无法回答 7-解锁提示）</param>
        /// <param name="message">附加消息</param>
        /// <returns></returns>
        Task AddAnswerLog(int uid, int gid, int pid, string answer, int status, string message);

        /// <summary>
        /// 将题目标记为已完成。并继续推进题目进度。
        /// </summary>
        /// <param name="uid">用户UID</param>
        /// <param name="username">用户名（用于推送消息生成）</param>
        /// <param name="gid">队伍GID</param>
        /// <param name="pid">题目PID</param>
        /// <param name="message">附加消息</param>
        /// <returns>code, answerStatus, extendFlag, message, location五元组。其中code为0或1，为1时表示失败。answerStatus表示判题结果，由于本功能只作为标记成功使用，所以只有1-正确，extendFlag为判题额外消息，为0-默认 1-跳转 16-重载当前页。message为返回消息。location为跳转时应当跳转的目标。</returns>
        Task<(int code, int answerStatus, int extendFlag, string message, string location)> MakePuzzleFinished(int uid, string username, int gid, int pid, string message);
    }
}
