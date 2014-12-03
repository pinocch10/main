using log4net;

namespace MovieSchedule.Core.Logging
{
    public static class Loggable
    {
        //public ILog Logger = LogManager.GetLogger(typeof(T));

        static Loggable()
        {
            // Configure log4net
            log4net.Config.XmlConfigurator.Configure();
        }

        public static ILog GetDefaultLogger(this ILoggable loggable)
        {
            return loggable.GetLogger("DefaultLogger");
        }

        public static ILog GetLogger(this ILoggable loggable, string loggerName)
        {
            return LogManager.GetLogger(loggerName);
        }

        public static ILog GetLogger(string loggerName = "")
        {
            return LogManager.GetLogger(string.IsNullOrWhiteSpace(loggerName) ? "DefaultLogger" : loggerName);
        }
    }
}