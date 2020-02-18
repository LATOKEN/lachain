using System;
using NLog;

namespace Phorkus.Logger
{
    public class LoggerAdapter<T> : ILogger<T>
    {
        #region Private fields

        private readonly NLog.Logger _logger;

        #endregion

        public LoggerAdapter(NLog.Logger logger)
        {
            _logger = logger;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _logger.IsEnabled(logLevel);
        }

        //------------------------------------------DEBUG------------------------------------------//

        /// <inheritdoc />
        public void LogDebug(Exception exception, string message, params object[] args)
        {
            Log(LogLevel.Debug, exception, message, args);
        }

        /// <inheritdoc />
        public void LogDebug(string message, params object[] args)
        {
            Log(LogLevel.Debug, message, args);
        }

        //------------------------------------------TRACE------------------------------------------//

        /// <inheritdoc />
        public void LogTrace(Exception exception, string message, params object[] args)
        {
            Log(LogLevel.Trace, exception, message, args);
        }

        /// <inheritdoc />
        public void LogTrace(string message, params object[] args)
        {
            Log(LogLevel.Trace, message, args);
        }

        //------------------------------------------INFORMATION------------------------------------------//

        /// <inheritdoc />
        public void LogInformation(Exception exception, string message, params object[] args)
        {
            Log(LogLevel.Info, exception, message, args);
        }

        /// <inheritdoc />
        public void LogInformation(string message, params object[] args)
        {
            Log(LogLevel.Info, message, args);
        }

        //------------------------------------------WARNING------------------------------------------//

        /// <inheritdoc />
        public void LogWarning(Exception exception, string message, params object[] args)
        {
            Log(LogLevel.Warn, exception, message, args);
        }

        /// <inheritdoc />
        public void LogWarning(string message, params object[] args)
        {
            Log(LogLevel.Warn, message, args);
        }

        //------------------------------------------ERROR------------------------------------------//

        /// <inheritdoc />
        public void LogError(Exception exception, string message, params object[] args)
        {
            Log(LogLevel.Error, exception, message, args);
        }

        /// <inheritdoc />
        public void LogError(string message, params object[] args)
        {
            Log(LogLevel.Error, message, args);
        }

        //------------------------------------------CRITICAL------------------------------------------//

        /// <inheritdoc />
        public void LogCritical(Exception exception, string message, params object[] args)
        {
            Log(LogLevel.Fatal, exception, message, args);
        }

        /// <inheritdoc />
        public void LogCritical(string message, params object[] args)
        {
            Log(LogLevel.Fatal, message, args);
        }

        /// <inheritdoc />
        public void Log(LogLevel logLevel, string message, params object[] args)
        {
            Log(logLevel, null, message, args);
        }

        /// <inheritdoc />
        public void Log(LogLevel logLevel, Exception? exception, string message, params object[] args)
        {
            _logger.Log(logLevel, exception, message, args);
        }
    }
}