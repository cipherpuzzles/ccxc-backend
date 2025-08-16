using ccxc_backend.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ccxc_backend.Functions.PowerPoint
{
    public class PowerPoint
    {
        public static async Task<int> GetPowerPoint(Progress progressDb, int gid)
        {
            var progress = await progressDb.SimpleDb.AsQueryable().Where(x => x.gid == gid).FirstAsync();
            if (progress == null)
            {
                throw new Exception("gid progress is not exists.");
            }

            var rate = RedisNumberCenter.PowerIncreaseRate;

            var nowPoint = progress.power_point + rate * (int)Math.Floor((DateTime.Now - progress.power_point_update_time).TotalMinutes);
            return nowPoint;
        }
        public static async Task UpdatePowerPoint(Progress progressDb, int gid, int offset)
        {
            var progress = await progressDb.SimpleDb.AsQueryable().Where(x => x.gid == gid).FirstAsync();
            if (progress == null)
            {
                throw new Exception("gid progress is not exists.");
            }

            var rate = RedisNumberCenter.PowerIncreaseRate;

            var now = DateTime.Now;
            var nowPoint = progress.power_point + rate * (int)Math.Floor((now - progress.power_point_update_time).TotalMinutes);
            var newPoint = nowPoint + offset;

            //update progress;
            progress.power_point = newPoint;
            progress.power_point_update_time = now;

            await progressDb.SimpleDb.AsUpdateable(progress).UpdateColumns(x => new
            {
                x.power_point,
                x.power_point_update_time
            }).ExecuteCommandAsync();

            Ccxc.Core.Utils.Logger.Info($"[Powerpoint Log] gid={gid}, offset={offset}, oldValue={nowPoint}, newValue={newPoint}");
        }
    }
}
