using Lachain.Core.CLI;
using Lachain.Proto;

namespace Lachain.Core.Config
{
    public interface IConfigManager
    {
        T? GetConfig<T>(string name) where T : class;

        string ConfigPath { get; }

        RunOptions CommandLineOptions { get; }

        void UpdateCheckpoint(Checkpoint checkpoint);
    }
}