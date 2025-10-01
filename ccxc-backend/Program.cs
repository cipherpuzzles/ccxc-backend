using System;
using System.IO;
using System.Reflection;

namespace ccxc_backend
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (System.Globalization.DateTimeFormatInfo.CurrentInfo != null)
            {
                var type = System.Globalization.DateTimeFormatInfo.CurrentInfo.GetType();
                var field = type.GetField("generalLongTimePattern", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(System.Globalization.DateTimeFormatInfo.CurrentInfo, "yyyy-MM-dd HH:mm:ss");
                }
            }

            // 检查是否有命令行参数
            if (args.Length > 0 && args[0].ToLower() == "initadmin")
            {
                // 进入管理员初始化模式
                InitAdminMode();
                return;
            }

            var startUp = new Startup();
            startUp.Run();
            startUp.Wait();
        }

        private static void InitAdminMode()
        {
            Console.WriteLine("========== 管理员初始化模式 ==========");

            // 检查锁文件
            var lockFilePath = ".initadmin.lock";
            if (File.Exists(lockFilePath))
            {
                Console.WriteLine("系统已配置过基础管理员。为了防止误操作，如果你确实要再次配置基础管理员，请删除.initadmin.lock文件");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
                return;
            }

            try
            {
                // 创建锁文件
                File.WriteAllText(lockFilePath, $"Created at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine("已创建锁文件，防止重复执行。");

                // 要求用户输入UID
                Console.WriteLine();
                Console.WriteLine("请输入要设置为管理员的用户UID：");
                Console.WriteLine("提示：如果您还没有UID，请先启动服务，创建用户，并在个人中心确认自己的UID。");
                Console.Write("UID: ");

                var uidInput = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(uidInput) || !int.TryParse(uidInput.Trim(), out int uid))
                {
                    Console.WriteLine("无效的UID格式，请输入正确的数字UID。");
                    Console.WriteLine("按任意键退出...");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"将要设置UID为 {uid} 的用户为管理员，请确认 (y/N): ");
                var confirm = Console.ReadLine();
                if (confirm?.ToLower() != "y")
                {
                    Console.WriteLine("操作已取消。");
                    Console.WriteLine("按任意键退出...");
                    Console.ReadKey();
                    return;
                }

                // 执行管理员设置
                SetUserAsAdmin(uid);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"操作失败：{ex.Message}");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }
        }

        private static void SetUserAsAdmin(int uid)
        {
            try
            {
                Console.WriteLine("正在初始化数据库...");

                // 1. 执行数据库初始化
                var dm = new DataServices.DbMaintenance(Config.Config.Options.DbConnStr, Config.Config.Options.RedisConnStr);
                dm.InitDatabase();
                Console.WriteLine("数据库初始化完成。");

                // 2. 检查用户是否存在
                Console.WriteLine($"正在检查UID {uid} 的用户是否存在...");
                var userDb = DataServices.DbFactory.Get<DataModels.User>();
                var user = userDb.SimpleDb.AsQueryable().Where(it => it.uid == uid).First();

                if (user == null)
                {
                    Console.WriteLine($"错误：UID {uid} 的用户不存在！");
                    Console.WriteLine("请确认UID是否正确，或先启动服务创建用户。");
                    Console.WriteLine("按任意键退出...");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"找到用户：{user.username} (UID: {user.uid})");

                // 3. 更新用户角色为管理员
                Console.WriteLine("正在设置管理员权限...");
                user.roleid = 5;
                user.update_time = DateTime.Now;

                // 4. 更新数据库并清除缓存
                userDb.SimpleDb.AsUpdateable(user)
                    .UpdateColumns(x => new { x.roleid, x.update_time })
                    .RemoveDataCache()
                    .ExecuteCommand();

                Console.WriteLine("========== 操作成功 ==========");
                Console.WriteLine($"用户 {user.username} (UID: {uid}) 已成功设置为管理员！");
                Console.WriteLine("角色ID已更新为5（管理员）");
                Console.WriteLine("请重新登录以获取管理员权限。");
                Console.WriteLine("=============================");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"设置管理员失败：{ex.Message}");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
                throw;
            }
        }
    }
}
