using log4net;
using log4net.Config;
using log4net.Repository;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Ccxc.Core.Utils
{
    public class Logger
    {
        public static Logger Instance { get; } = new Logger();

        public ILog Log;

        private Logger()
        {
                string log4netConfigXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <!-- This section contains the log4net configuration settings -->
  <log4net>
    <appender name=""ConsoleAppender"" type=""log4net.Appender.ConsoleAppender"">
      <layout type=""log4net.Layout.PatternLayout"" value=""[%-4thread] %date %-5level - %message%newline"" />
      <filter type=""log4net.Filter.LevelMatchFilter"">
        <levelToMatch  value=""INFO"" />
      </filter>
      <filter type=""log4net.Filter.LevelMatchFilter"">
        <levelToMatch  value=""WARN"" />
      </filter>
      <filter type=""log4net.Filter.LevelMatchFilter"">
        <levelToMatch  value=""ERROR"" />
      </filter>
      <filter type=""log4net.Filter.LevelMatchFilter"">
        <levelToMatch  value=""FATAL"" />
      </filter>
      <filter type=""log4net.Filter.DenyAllFilter"" />
    </appender>

    <appender name=""RollingLogFileAppender"" type=""log4net.Appender.RollingFileAppender"">
      <file value=""log/"" />
      <appendToFile value=""true"" />
      <rollingStyle value=""Composite"" />
      <staticLogFileName value=""false"" />
      <datePattern value=""yyyyMMdd'.log'"" />
      <maxSizeRollBackups value=""20"" />
      <maximumFileSize value=""10MB"" />
      <layout type=""log4net.Layout.PatternLayout"">
        <conversionPattern value=""[%-4thread] %date %-5level - %message%newline"" />
      </layout>
    </appender>

    <appender name=""ErrorFileAppender"" type=""log4net.Appender.RollingFileAppender"">
      <file value=""log/ERROR_"" />
      <appendToFile value=""true"" />
      <rollingStyle value=""Composite"" />
      <staticLogFileName value=""false"" />
      <datePattern value=""yyyyMMdd'.log'"" />
      <maxSizeRollBackups value=""20"" />
      <maximumFileSize value=""10MB"" />
      <layout type=""log4net.Layout.PatternLayout"">
        <conversionPattern value=""[%-4thread] %date %-5level - %message%newline"" />
      </layout>
      <filter type=""log4net.Filter.LevelMatchFilter"">
        <levelToMatch  value=""ERROR"" />
      </filter>
      <filter type=""log4net.Filter.LevelMatchFilter"">
        <levelToMatch  value=""FATAL"" />
      </filter>
      <filter type=""log4net.Filter.DenyAllFilter"" />
    </appender>

    <!-- Setup the root category, add the appenders and set the default level -->
    <root>
      <level value=""ALL"" />
      <appender-ref ref=""ConsoleAppender"" />
      <appender-ref ref=""RollingLogFileAppender"" />
      <appender-ref ref=""ErrorFileAppender"" />
    </root>

  </log4net>
</configuration>

";
            using var configStream = new MemoryStream();
            var configBytes = Encoding.UTF8.GetBytes(log4netConfigXml);
            configStream.Write(configBytes, 0, configBytes.Length);
            configStream.Position = 0;


            ILoggerRepository repository = LogManager.CreateRepository("lrs");
            XmlConfigurator.Configure(repository, configStream);
            Log = LogManager.GetLogger(repository.Name, "Logger");
        }

        public static void Debug(object msg)
        {
            Instance.Log?.Debug(msg);
        }

        public static Task DebugAsync(object msg)
        {
            return Task.Run(() => Debug(msg));
        }

        public static void Info(object msg)
        {
            Instance.Log?.Info(msg);
        }

        public static Task InfoAsync(object msg)
        {
            return Task.Run(() => Info(msg));
        }

        public static void Warn(object msg)
        {
            Instance.Log?.Warn(msg);
        }

        public static Task WarnAsync(object msg)
        {
            return Task.Run(() => Warn(msg));
        }

        public static void Error(object msg)
        {
            Instance.Log?.Error(msg);
        }

        public static Task ErrorAsync(object msg)
        {
            return Task.Run(() => Error(msg));
        }

        public static void Fatal(object msg)
        {
            Instance.Log?.Fatal(msg);
        }

        public static Task FatalAsync(object msg)
        {
            return Task.Run(() => Fatal(msg));
        }
    }
}
