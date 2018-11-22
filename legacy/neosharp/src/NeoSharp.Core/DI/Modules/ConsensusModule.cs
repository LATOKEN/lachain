using NeoSharp.Core.Consensus;
using NeoSharp.Core.Consensus.Config;

namespace NeoSharp.Core.DI.Modules
{
    public class ConsensusModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder)
        {
            containerBuilder.RegisterSingleton<ConsensusConfig>();
            containerBuilder.RegisterSingleton<IConsensusManager, ConsensusManager>();
        }
    }
}