namespace Lachain.Logger
{
    public static class LoggerFactory
    {
        public static ILogger<T> GetLoggerForClass<T>()
        {
            return new LoggerAdapter<T>(NLog.LogManager.GetLogger(typeof(T).Name));
        }
    }
}