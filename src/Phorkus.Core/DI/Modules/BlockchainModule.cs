using Phorkus.Core.Blockchain;
using Phorkus.Core.CLI;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Core.Blockchain.OperationManager;
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
            containerBuilder.RegisterSingleton<IConsensusManager, ConsensusManager>();
            /* gensis */
            containerBuilder.RegisterSingleton<IGenesisBuilder, GenesisBuilder>();
            /* operation manager */
            containerBuilder.RegisterSingleton<ITransactionManager, TransactionManager>();
            containerBuilder.RegisterSingleton<IBlockManager, BlockManager>();
            /* pool */
            containerBuilder.RegisterSingleton<ITransactionPool, TransactionPool>();
            /* rpc */
            containerBuilder.RegisterSingleton<IRpcManager, RpcManager>();
            /* vm */
            containerBuilder.RegisterSingleton<IVirtualMachine, VirtualMachine>();
        }
    }
}