using Lachain.Core.Blockchain.Genesis;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Utils;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Vault;
using Microsoft.Extensions.DependencyInjection;

namespace Lachain.Core.DI
{
    public static class BlockchainModule
    {
        public static IServiceCollection AddServices(IServiceCollection services)
        {
            return services
                .AddSingleton<ITransactionVerifier, TransactionVerifier>()
                .AddSingleton<ITransactionBuilder, TransactionBuilder>()
                .AddSingleton<IMultisigVerifier, MultisigVerifier>()
                .AddSingleton<IPrivateWallet, PrivateWallet>()
                .AddSingleton<IGenesisBuilder, GenesisBuilder>()
                .AddSingleton<ITransactionManager, TransactionManager>()
                .AddSingleton<ITransactionSigner, TransactionSigner>()
                .AddSingleton<ISystemContractReader, SystemContractReader>()
                .AddSingleton<IBlockManager, BlockManager>()
                .AddSingleton<IContractRegisterer, ContractRegisterer>()
                .AddSingleton<ITransactionPool, TransactionPool>()
                .AddSingleton<IVirtualMachine, VirtualMachine>();
                // .AddSingleton<IContractInvoker, ContractInvoker>(); TODO: this should be in DI but it's static
        }
    }
}