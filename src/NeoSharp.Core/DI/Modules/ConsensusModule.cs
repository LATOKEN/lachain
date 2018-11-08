using NeoSharp.Core.Consensus;

namespace NeoSharp.Core.DI.Modules
{
    public class ConsensusModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder)
        {
            containerBuilder.RegisterSingleton<IConsensusManager, ConsensusManager>();
        }
    }
}