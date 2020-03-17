using Phorkus.Consensus;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.ContractManager;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Core.Blockchain.Interface;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.Pool;
using Phorkus.Core.Blockchain.Validators;
using Phorkus.Core.CLI;
using Phorkus.Core.Config;
using Phorkus.Core.Consensus;
using Phorkus.Core.RPC;
using Phorkus.Core.VM;

namespace Phorkus.Core.DI.Modules
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
            containerBuilder.RegisterSingleton<IValidatorManager, ValidatorManager>();
            containerBuilder.RegisterSingleton<IMultisigVerifier, MultisigVerifier>();
            /* consensus */
            containerBuilder.RegisterSingleton<IBlockProducer, BlockProducer>();
            containerBuilder.RegisterSingleton<IConsensusManager, ConsensusManager>();
            containerBuilder.RegisterSingleton<IValidatorManager, ValidatorManager>();
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