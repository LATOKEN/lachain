namespace Phorkus.Core.Config
{
    public interface IConfigManager
    {
        T? GetConfig<T>(string name) where T : class;
    }
}