using Serilog;
using Serilog.Events;

namespace ImageSorter
{
    public class LogUtility
    {
        #region Level enum

        public enum Level
        {
            Info,
            Debug,
            Warn,
            Error,
            Fatal
        }

        #endregion

        public static void Init()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("Logs/imagesorter-.log", rollingInterval: RollingInterval.Hour)
                .CreateLogger();
        }

        public static void WriteToLog(string message, Level level)
        {
            WriteToLog(message, level, null);
        }

        public static void WriteToLog(string message, Level level, Exception? exception)
        {
            var eventLevel = level switch
            {
                Level.Info => LogEventLevel.Information,
                Level.Debug => LogEventLevel.Debug,
                Level.Warn => LogEventLevel.Warning,
                Level.Error => LogEventLevel.Error,
                Level.Fatal => LogEventLevel.Fatal,
                _ => LogEventLevel.Information
            };

            Log.Write(eventLevel, exception, message);
        }

        public static void LogDuplicate(string message)
        {
            WriteToLog(message, Level.Info);
        }

        public static void LogMoved(string message)
        {
            WriteToLog(message, Level.Info);
        }
    }
}
