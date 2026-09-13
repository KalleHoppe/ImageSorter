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

        // One fixed file per run (rather than Serilog's hourly-rolling shared file) so the path
        // reported at the end of a run unambiguously points at that run's own log.
        public static string CurrentLogFilePath { get; private set; } = string.Empty;
        public static string CurrentLogFileUri => new Uri(CurrentLogFilePath).AbsoluteUri;

        public static void Init()
        {
            CurrentLogFilePath = Path.GetFullPath(Path.Combine("Logs", $"imagesorter-{DateTime.Now:yyyyMMdd_HHmmss}.log"));

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(CurrentLogFilePath)
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

        public static void LogMoved(string message)
        {
            WriteToLog(message, Level.Info);
        }
    }
}
