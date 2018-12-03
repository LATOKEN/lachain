namespace Phorkus.Logger
{
    public interface ILoggerProvider<T>
    {
        void LogWarning(string warningMessage);
    }
}