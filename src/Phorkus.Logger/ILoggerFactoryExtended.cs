using Microsoft.Extensions.Logging;

namespace Phorkus.Logger
{
    public delegate void DelOnLog(LogEntry log);

    public interface ILoggerFactoryExtended : ILoggerFactory
    {
        event DelOnLog OnLog;

        void RaiseOnLog(LogEntry log);
    }
}