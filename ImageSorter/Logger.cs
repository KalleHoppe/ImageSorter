using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

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

        public enum LoggerName
        {
            AppLogLogger
        }

        #endregion

        public static void WriteToLog(string message, Level level)
        {
            WriteToLog(message, "AppLogLogger", level);
        }

        public static void WriteToLog(string message, Level level, Exception ex)
        {
            WriteToLog(message, "AppLogLogger", level, ex);
        }

        public static void LogDuplicate(string message)
        {
            WriteToLog(message, "DuplicateLogger", Level.Info);
        }

        public static void LogMoved(string message)
        {
            WriteToLog(message, "MovedLogger", Level.Info);
        }

        public static void WriteToLog(string message, string logger, Level level)
        {
            WriteToLog(message, logger, level, null);
        }

        public static void WriteToLog(string message, string logger, Level level, Exception exception)
        {
            ILog logToWriteTo = LogManager.GetLogger(logger);

            switch (level)
            {
                case Level.Info:
                    logToWriteTo.Info(message, exception);
                    break;
                case Level.Debug:
                    logToWriteTo.Debug(message, exception);
                    break;
                case Level.Warn:
                    logToWriteTo.Warn(message, exception);
                    break;
                case Level.Error:
                    logToWriteTo.Error(message, exception);
                    break;
                case Level.Fatal:
                    logToWriteTo.Fatal(message, exception);
                    break;
            }
        }
    }
}
