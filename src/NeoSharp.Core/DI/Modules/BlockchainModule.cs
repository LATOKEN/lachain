using System.Collections.Generic;
using NeoSharp.Core.Blockchain;
using NeoSharp.Core.Blockchain.Genesis;
using NeoSharp.Core.Blockchain.Processing;
using NeoSharp.Core.Blockchain.Processing.BlockHeaderProcessing;
using NeoSharp.Core.Blockchain.Processing.BlockProcessing;
using NeoSharp.Core.Blockchain.Processing.TranscationProcessing;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.OperationManager;
using NeoSharp.Core.Models.Transactions;

namespace NeoSharp.Core.DI.Modules
{
    public class BlockchainModule : IModule
    {
        public void Register(IContainerBuilder containerBuilder)
        {
            containerBuilder.RegisterSingleton<IGenesisBuilder, GenesisBuilder>();
            containerBuilder.RegisterSingleton<IGenesisAssetsBuilder, GenesisAssetsBuilder>();

            containerBuilder.RegisterSingleton<IBlockchain, Blockchain.Blockchain>();

            #region Processing
            containerBuilder.RegisterSingleton<IBlockPersister, BlockPersister>();
            containerBuilder.RegisterSingleton<IBlockHeaderPersister, BlockHeaderPersister>();
            containerBuilder.RegisterSingleton<IBlockProcessor, BlockProcessor>();
            containerBuilder.RegisterSingleton<IBlockPool, BlockPool>();
            containerBuilder.RegisterSingleton<IComparer<Transaction>, TransactionComparer>();
            containerBuilder.RegisterSingleton<ITransactionPool, TransactionPool>();
            containerBuilder.RegisterSingleton<IBlockProducer, BlockProducer>();
            containerBuilder.RegisterSingleton<ITransactionPersister<Transaction>, TransactionPersister>();
            containerBuilder.RegisterSingleton<IBlockHeaderValidator, BlockHeaderValidator>();

            containerBuilder.RegisterSingleton<ITransactionPersister<IssueTransaction>, IssueTransactionPersister>();
            containerBuilder.RegisterSingleton<ITransactionPersister<ContractTransaction>, ContractTransactionPersister>();
            containerBuilder.RegisterSingleton<ITransactionPersister<PublishTransaction>, PublishTransactionPersister>();
            containerBuilder.RegisterSingleton<ITransactionPersister<RegisterTransaction>, RegisterTransactionPersister>();

            containerBuilder.RegisterSingleton<ISigner<BlockHeader>, BlockHeaderOperationsManager>();
            containerBuilder.RegisterSingleton<IVerifier<BlockHeader>, BlockHeaderOperationsManager>();
            containerBuilder.RegisterSingleton<IBlockHeaderOperationsManager, BlockHeaderOperationsManager>();

            containerBuilder.RegisterSingleton<ISigner<Block>, BlockOperationManager>();
            containerBuilder.RegisterSingleton<IVerifier<Block>, BlockOperationManager>();
            containerBuilder.RegisterSingleton<IBlockOperationsManager, BlockOperationManager>();

            containerBuilder.RegisterSingleton<ISigner<Transaction>, TransactionOperationManager>();
            containerBuilder.RegisterSingleton<IVerifier<Transaction>, TransactionOperationManager>();
            containerBuilder.RegisterSingleton<ITransactionOperationsManager, TransactionOperationManager>();
            
            containerBuilder.RegisterSingleton<ITransactionProcessor, TransactionProcessor>();
            #endregion
        }
    }
}