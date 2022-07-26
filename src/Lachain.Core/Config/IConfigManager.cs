using Lachain.Core.CLI;
using Lachain.Proto;
using System.Collections.Generic;

namespace Lachain.Core.Config
{
    public interface IConfigManager
    {
        T? GetConfig<T>(string name) where T : class;

        string ConfigPath { get; }

        RunOptions CommandLineOptions { get; }
        void UpdateCheckpointConfig(List<CheckpointConfigInfo> checkpoints);
    }
}