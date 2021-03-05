using Lachain.Consensus;
using Lachain.Core.Blockchain.Validators;
using Lachain.Core.Consensus;
using Lachain.Core.Network;
using Lachain.Core.ValidatorStatus;
using Lachain.Core.Vault;
using Lachain.Networking;
using Microsoft.Extensions.DependencyInjection;

namespace Lachain.Core.DI
{
    public static class ConsensusModule
    {
        public static IServiceCollection AddServices(IServiceCollection services)
        {
            return services
                .AddSingleton<IBlockProducer, BlockProducer>()
                .AddSingleton<IConsensusManager, ConsensusManager>()
                .AddSingleton<IConsensusMessageDeliverer, NetworkManager>()
                .AddSingleton<IValidatorManager, ValidatorManager>()
                .AddSingleton<IKeyGenManager, KeyGenManager>()
                .AddSingleton<IValidatorStatusManager, ValidatorStatusManager>();
        }
    }
}