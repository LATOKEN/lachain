using Lachain.Consensus;
using Lachain.Core.Blockchain;
using Lachain.Core.Blockchain.ContractManager;
using Lachain.Core.Blockchain.Genesis;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.OperationManager;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.Validators;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.Consensus;
using Lachain.Core.RPC;
using Lachain.Core.Vault;
using Lachain.Core.VM;

namespace Lachain.Core.DI.Modules
{
    public class BlockchainModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            /* global */
            containerBuilder.RegisterSingleton<IBlockchainManager, BlockchainManager>();
            containerBuilder.RegisterSingleton<IConsoleManager, ConsoleManager>();
            containerBuilder.RegisterSingleton<ITransactionVerifier, TransactionVerifier>();
            containerBuilder.RegisterSingleton<ITransactionBuilder, TransactionBuilder>();
            containerBuilder.RegisterSingleton<IMultisigVerifier, MultisigVerifier>();
            /* consensus */
            containerBuilder.RegisterSingleton<IBlockProducer, BlockProducer>();
            containerBuilder.RegisterSingleton<IConsensusManager, ConsensusManager>();
            containerBuilder.RegisterSingleton<IValidatorManager, ValidatorManager>();
            containerBuilder.RegisterSingleton<IPrivateWallet, PrivateWallet>();
            containerBuilder.RegisterSingleton<KeyGenManager, KeyGenManager>();
            /* genesis */
            containerBuilder.RegisterSingleton<IGenesisBuilder, GenesisBuilder>();
            /* operation manager */
            containerBuilder.RegisterSingleton<ITransactionManager, TransactionManager>();
            containerBuilder.RegisterSingleton<IBlockManager, BlockManager>();
            containerBuilder.RegisterSingleton<IContractRegisterer, ContractRegisterer>();
            containerBuilder.RegisterSingleton<ITransactionPool, TransactionPool>();
            /* RPC */
            containerBuilder.RegisterSingleton<IRpcManager, RpcManager>();
            /* VM */
            containerBuilder.RegisterSingleton<IVirtualMachine, VirtualMachine>();
        }
    }
}