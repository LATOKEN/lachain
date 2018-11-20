using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using Phorkus.Core.Logging;

namespace Phorkus.Logger
{
    using ILogger = Microsoft.Extensions.Logging.ILogger;
    
    public class NLogLoggerFactory : ILoggerFactoryExtended
    {
        public event DelOnLog OnLog;

        private readonly LoggerFactory _loggerFactory;

        /// <summary>
        /// Constructor
        /// </summary>
        public NLogLoggerFactory()
        {
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddNLog(new NLogProviderOptions
            {
                CaptureMessageTemplates = true,
                CaptureMessageProperties = true
            });
            LogManager.LoadConfiguration("./nlog.config");
        }

        public void Dispose()
        {
            _loggerFactory.Dispose();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggerFactory.CreateLogger(categoryName);
        }

        public void RaiseOnLog(LogEntry log)
        {
            OnLog?.Invoke(log);
        }

        public void AddProvider(ILoggerProvider provider)
        {
            _loggerFactory.AddProvider(provider);
        }
    }
}