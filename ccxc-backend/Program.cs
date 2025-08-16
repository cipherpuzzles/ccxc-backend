using System;
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

            var startUp = new Startup();
            startUp.Run();
            startUp.Wait();
        }
    }
}
