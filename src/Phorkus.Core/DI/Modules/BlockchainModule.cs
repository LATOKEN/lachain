using Phorkus.Core.Blockchain;
using Phorkus.Core.CLI;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.OperationManager.BlockManager;
using Phorkus.Core.Blockchain.OperationManager.TransactionManager;
using Phorkus.Core.Config;
using Phorkus.Core.Consensus;
using Phorkus.Core.CrossChain;
using Phorkus.Core.Threshold;
using Phorkus.CrossChain;

namespace Phorkus.Core.DI.Modules
{
    public class BlockchainModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder, IConfigManager configManager)
        {
            /* global */
            containerBuilder.RegisterSingleton<IBlockchainManager, BlockchainManager>();
            
            
            
            containerBuilder.RegisterSingleton<ICLI, CLI.CLI>();
            containerBuilder.RegisterSingleton<ITransactionVerifier, TransactionVerifier>();
            containerBuilder.RegisterSingleton<ITransactionBuilder, TransactionBuilder>();
            containerBuilder.RegisterSingleton<IValidatorManager, ValidatorManager>();
            containerBuilder.RegisterSingleton<IMultisigVerifier, MultisigVerifier>();
            /* consensus */
            containerBuilder.RegisterSingleton<IConsensusManager, ConsensusManager>();
            /* gensis */
            containerBuilder.RegisterSingleton<IGenesisAssetsBuilder, GenesisAssetsBuilder>();
            containerBuilder.RegisterSingleton<IGenesisBuilder, GenesisBuilder>();
            /* operation manager */
            containerBuilder.RegisterSingleton<ICrossChain, CrossChain.CrossChain>();
            containerBuilder.RegisterSingleton<ICrossChainManager, CrossChainManager>();
            containerBuilder.RegisterSingleton<ITransactionManager, TransactionManager>();
            containerBuilder.RegisterSingleton<IWithdrawalManager, WithdrawalManager>();
            containerBuilder.RegisterSingleton<IBlockManager, BlockManager>();
            /* pool */
            containerBuilder.RegisterSingleton<ITransactionPool, TransactionPool>();
            containerBuilder.RegisterSingleton<IThresholdManager, ThresholdManager>();
        }
    }
}