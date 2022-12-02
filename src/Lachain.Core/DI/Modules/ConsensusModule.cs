using Lachain.Consensus;
using Lachain.Consensus.Messages;
using Lachain.Core.Blockchain.Validators;
using Lachain.Core.Config;
using Lachain.Core.Consensus;
using Lachain.Core.ValidatorStatus;
using Lachain.Core.Vault;

namespace Lachain.Core.DI.Modules
{
    public class ConsensusModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            containerBuilder.RegisterSingleton<IBlockProducer, BlockProducer>();
            containerBuilder.RegisterSingleton<IConsensusManager, ConsensusManager>();
            containerBuilder.RegisterSingleton<IValidatorManager, ValidatorManager>();
            containerBuilder.RegisterSingleton<IKeyGenManager, KeyGenManager>();
            containerBuilder.RegisterSingleton<IValidatorStatusManager, ValidatorStatusManager>();
            containerBuilder.RegisterSingleton<IMessageEnvelopeRepositoryManager, MessageEnvelopeRepositoryManager>();
        }
    }
}