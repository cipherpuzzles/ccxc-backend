using Aliyun.Acs.Core;
using Aliyun.Acs.Core.Profile;
using Aliyun.Acs.Dm.Model.V20170622;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Functions
{
    public static class EmailSender
    {
        // TODO: 为了实现你自己的邮件发送功能，请根据实际情况更新这里的代码。并配置 AliyunDmAccessKey 和 AliyunDmAccessSecret。
        // 如果你不想用阿里云，那可能得自己实现发送邮件的逻辑了。
        public static Task<bool> SendVerify(string email, string token)
        {
            return Task.Run(() =>
            {
                var regionId = "ap-southeast-1";
                var regionHost = "dm.ap-southeast-1.aliyuncs.com";

                IClientProfile profile = DefaultProfile.GetProfile(regionId, Config.Config.Options.AliyunDmAccessKey, Config.Config.Options.AliyunDmAccessSecret);
                profile.AddEndpoint(regionHost, regionId, "Dm", regionHost);

                IAcsClient client = new DefaultAcsClient(profile);
                var request = new SingleSendMailRequest();

                var webPrefix = Config.SystemConfigLoader.Config.ProjectFrontendPrefix;

                try
                {
                    request.AccountName = "noreply@notice.cipherpuzzles.com";
                    request.FromAlias = "密码菌（请勿回复本地址）";
                    request.AddressType = 1;
                    request.TagName = "restorePass";
                    request.ReplyToAddress = false;
                    request.ToAddress = email;
                    request.Subject = "CCBC 重置密码验证";
                    request.HtmlBody = $@"
<p>尊敬的用户：</p>
<p>&nbsp;</p>
<p>您收到此邮件是因为您在密码菌网站中尝试进行密码重置。本邮件将会引导您完成之后的步骤。</p>
<p>&nbsp;</p>
<p>如果不是您申请的密码重置，可能是其他人错误的填写了您的邮件地址，请忽略本邮件。</p>
<p>要继续密码重置，请<a href=""{webPrefix}/user/resetpass?token={token}"" target=""_blank"">点击此链接</a>进入密码重置页，然后在密码重置页上输入您的新密码。</p>
<p>&nbsp;</p>
<p>如果您点击以上链接无效，请尝试将以下链接复制到您的浏览器并打开：</p>
<p>{webPrefix}/user/resetpass?token={token}</p>
<p>&nbsp;</p>
<p>祝参赛愉快。</p>
<p>Cipherpuzzles.com 密码菌</p>
<hr>
<p>请勿回复本邮件，如有问题可发送邮件至info@cipherpuzzles.com咨询。</p>
<p>请关注微信公众号【密码菌】持续获取资讯</p>";
                    var response = client.GetAcsResponse(request);

                    return true;
                }
                catch (Exception e)
                {
                    Ccxc.Core.Utils.Logger.Error(e.ToString());
                    return false;
                }
            });
        }

        public static Task<bool> EmailVerify(string email, string token)
        {
            return Task.Run(() =>
            {
                var regionId = "ap-southeast-1";
                var regionHost = "dm.ap-southeast-1.aliyuncs.com";

                IClientProfile profile = DefaultProfile.GetProfile(regionId, Config.Config.Options.AliyunDmAccessKey, Config.Config.Options.AliyunDmAccessSecret);
                profile.AddEndpoint(regionHost, regionId, "Dm", regionHost);

                IAcsClient client = new DefaultAcsClient(profile);
                var request = new SingleSendMailRequest();

                var webPrefix = Config.SystemConfigLoader.Config.ProjectFrontendPrefix;

                try
                {
                    request.AccountName = "noreply@notice.cipherpuzzles.com";
                    request.FromAlias = "密码菌（请勿回复本地址）";
                    request.AddressType = 1;
                    request.TagName = "emailVerify";
                    request.ReplyToAddress = false;
                    request.ToAddress = email;
                    request.Subject = "CCBC 邮箱验证";
                    request.HtmlBody = $@"
<p>尊敬的用户：</p>
<p>&nbsp;</p>
<p>感谢您报名参与 CCBC！本邮件将引导您完成Email验证和账号激活。</p>
<p>&nbsp;</p>
<p>如果不是您正在注册或激活 CCBC，可能是其他人错误的填写了您的邮件地址，请忽略本邮件。</p>
<p>要继续完成Email验证，请<a href=""{webPrefix}/user/emailverify?token={token}"" target=""_blank"">点击此链接</a>。并按屏幕上的指示操作。</p>
<p>一切正常的话，您只需重新登录。</p>
<p>&nbsp;</p>
<p>如果您点击以上链接无效，请尝试将以下链接复制到您的浏览器并打开：</p>
<p>{webPrefix}/user/emailverify?token={token}</p>
<p>&nbsp;</p>
<p>祝参赛愉快。</p>
<p>Cipherpuzzles.com 密码菌</p>
<hr>
<p>请勿回复本邮件，如有问题可发送邮件至info@cipherpuzzles.com咨询。</p>
<p>请关注微信公众号【密码菌】持续获取资讯</p>";
                    var response = client.GetAcsResponse(request);

                    return true;
                }
                catch (Exception e)
                {
                    Ccxc.Core.Utils.Logger.Error(e.ToString());
                    return false;
                }
            });
        }
    }
}
