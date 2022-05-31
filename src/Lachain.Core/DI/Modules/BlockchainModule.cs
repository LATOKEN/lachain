using Lachain.Core.Blockchain.Genesis;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Utils;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Config;
using Lachain.Core.Vault;

namespace Lachain.Core.DI.Modules
{
    public class BlockchainModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            /* global */
            containerBuilder.RegisterSingleton<ITransactionVerifier, TransactionVerifier>();
            containerBuilder.RegisterSingleton<ITransactionBuilder, TransactionBuilder>();
            containerBuilder.RegisterSingleton<IMultisigVerifier, MultisigVerifier>();
            containerBuilder.RegisterSingleton<IPrivateWallet, PrivateWallet>();
            /* genesis */
            containerBuilder.RegisterSingleton<IGenesisBuilder, GenesisBuilder>();
            /* operation manager */
            containerBuilder.RegisterSingleton<ITransactionManager, TransactionManager>();
            containerBuilder.RegisterSingleton<ITransactionSigner, TransactionSigner>();
            containerBuilder.RegisterSingleton<ISystemContractReader, SystemContractReader>();
            containerBuilder.RegisterSingleton<IBlockManager, BlockManager>();
            containerBuilder.RegisterSingleton<IContractRegisterer, ContractRegisterer>();
            containerBuilder.RegisterSingleton<ITransactionPool, TransactionPool>();
            containerBuilder.RegisterSingleton<INonceCalculator, NonceCalculator>();
            containerBuilder.RegisterSingleton<ITransactionHashTrackerByNonce, TransactionHashTrackerByNonce>();
            /* VM */
            containerBuilder.RegisterSingleton<IVirtualMachine, VirtualMachine>();
            containerBuilder.RegisterSingleton<IContractInvoker, ContractInvoker>();
        }
    }
}