using System;
using System.Linq;
using System.Threading;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Validators;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.Consensus;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Network;
using Lachain.Core.RPC;
using Lachain.Core.ValidatorStatus;
using Lachain.Core.Vault;
using Lachain.Core.Network.FastSynchronizerBatch;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Networking;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Storage.Trie;
using Lachain.Utility.Utils;
using NLog;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using Lachain.Storage;
using Lachain.Core.Blockchain;
using Lachain.Storage.DbCompact;

namespace Lachain.Console
{
    public class Application : IBootstrapper, IDisposable
    {
        private readonly IContainer _container;
        private static readonly ILogger<Application> Logger = LoggerFactory.GetLoggerForClass<Application>();

        public Application(string configPath, RunOptions options)
        {
            var logLevel = options.LogLevel ?? Environment.GetEnvironmentVariable("LOG_LEVEL");
            if (logLevel != null) logLevel = char.ToUpper(logLevel[0]) + logLevel.ToLower().Substring(1);
            if (!new[] {"Trace", "Debug", "Info", "Warn", "Error", "Fatal"}.Contains(logLevel))
                logLevel = "Info";
            LogManager.Configuration.Variables["consoleLogLevel"] = logLevel;
            LogManager.ReconfigExistingLoggers();

            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(configPath, options));
            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<ConsensusModule>();
            containerBuilder.RegisterModule<NetworkModule>();
            containerBuilder.RegisterModule<StorageModule>();
            containerBuilder.RegisterModule<RpcModule>();
            containerBuilder.RegisterModule<ConsoleModule>();
            _container = containerBuilder.Build();
        }

        public void Start(RunOptions options)
        {
            System.Console.WriteLine("Testing prioritizing message: test 1");
            var configManager = _container.Resolve<IConfigManager>();
            var blockManager = _container.Resolve<IBlockManager>();
            var consensusManager = _container.Resolve<IConsensusManager>();
            var validatorStatusManager = _container.Resolve<IValidatorStatusManager>();
            var transactionVerifier = _container.Resolve<ITransactionVerifier>();
            var validatorManager = _container.Resolve<IValidatorManager>();
            var blockSynchronizer = _container.Resolve<IBlockSynchronizer>();
            var networkManager = _container.Resolve<INetworkManager>();
            var commandManager = _container.Resolve<IConsoleManager>();
            var rpcManager = _container.Resolve<IRpcManager>();
            var stateManager = _container.Resolve<IStateManager>();
            var wallet = _container.Resolve<IPrivateWallet>();
            var metricsService = _container.Resolve<IMetricsService>();
            var snapshotIndexRepository = _container.Resolve<ISnapshotIndexRepository>();
            var localTransactionRepository = _container.Resolve<ILocalTransactionRepository>();
            var NodeRetrieval = _container.Resolve<INodeRetrieval>();
            var dbContext = _container.Resolve<IRocksDbContext>();
            var storageManager = _container.Resolve<IStorageManager>();
            var transactionPool = _container.Resolve<ITransactionPool>();

            // check if compacting db was started but not finished
            var dbShrink = _container.Resolve<IDbShrink>();
            if(!dbShrink.IsStopped()) throw new Exception($"Compacting db was started with depth: {dbShrink.GetDbShrinkDepth()}"
                 + " by deleting nodes from old snapshot but was not finished.");

            // set chainId from config
            var chainId = configManager.GetConfig<NetworkConfig>("network")?.ChainId ?? 0;
            if (chainId == 0) throw new Exception("chainId is not defined in the config file");
            var newChainId = configManager.GetConfig<NetworkConfig>("network")?.NewChainId ?? 0;
            if (newChainId == 0) throw new Exception("newChainId is not defined in the config file");

            Logger.LogInformation($"ChainId {chainId}, newChainId {newChainId}");
            TransactionUtils.SetChainId(chainId, newChainId);

            var version = Assembly.GetEntryAssembly()!
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion;
            Logger.LogInformation($"Version: {version}");

            // set cycle and validatorCount
            StakingContract.Initialize(configManager.GetConfig<NetworkConfig>("network"));

            // set hardfork heights
            Logger.LogInformation($"Setting hardfork heights.");
            var hardforkConfig = configManager.GetConfig<HardforkConfig>("hardfork") ??
                    throw new Exception("No 'hardfork' section in config file");
            HardforkHeights.SetHardforkHeights(hardforkConfig);

            rpcManager.Start();
            
            if (options.RollBackTo.HasValue)
            {
                Logger.LogWarning($"Performing roll back to block {options.RollBackTo.Value}");
                var snapshot = snapshotIndexRepository.GetSnapshotForBlock(options.RollBackTo.Value);
                stateManager.RollbackTo(snapshot);
                wallet.DeleteKeysAfterBlock(options.RollBackTo.Value);
                stateManager.Commit();
                Logger.LogWarning($"Rollback to block {options.RollBackTo.Value} complete");
            }

        
            localTransactionRepository.SetWatchAddress(wallet.EcdsaKeyPair.PublicKey.GetAddress()); 

            if (blockManager.TryBuildGenesisBlock())
                Logger.LogInformation("Generated genesis block");

            var genesisBlock = stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(0)
                               ?? throw new Exception("Genesis block was not persisted");
            Logger.LogInformation("Genesis Block: " + genesisBlock.Hash.ToHex());
            Logger.LogInformation($" + prevBlockHash: {genesisBlock.Header.PrevBlockHash.ToHex()}");
            Logger.LogInformation($" + merkleRoot: {genesisBlock.Header.MerkleRoot.ToHex()}");
            Logger.LogInformation($" + nonce: {genesisBlock.Header.Nonce}");
            Logger.LogInformation($" + transactionHashes: {genesisBlock.TransactionHashes.ToArray().Length}");
            foreach (var s in genesisBlock.TransactionHashes)
                Logger.LogInformation($" + - {s.ToHex()}");
            Logger.LogInformation($" + hash: {genesisBlock.Hash.ToHex()}");

            Logger.LogInformation("Current block height: " + blockManager.GetHeight());
            Logger.LogInformation($"Node public key: {wallet.EcdsaKeyPair.PublicKey.EncodeCompressed().ToHex()}");
            Logger.LogInformation($"Node address: {wallet.EcdsaKeyPair.PublicKey.GetAddress().ToHex()}");

            if (options.SetStateTo.Any())
            {
                List<string> args = options.SetStateTo.ToList();
            //    System.Console.WriteLine(args);
                ulong blockNumber = 0;
                if( !(args is null) && args.Count>0)
                {
                    blockNumber = Convert.ToUInt64(args[0]);
                }
                FastSynchronizerBatch.StartSync(stateManager, dbContext, snapshotIndexRepository,
                                                storageManager.GetVersionFactory(), blockNumber);

            }
            /*    if(blockManager.GetHeight()==0)
                FastSynchronizerBatch.StartSync(stateManager, dbContext, snapshotIndexRepository,
                                                storageManager.GetVersionFactory(), 0); */

            var networkConfig = configManager.GetConfig<NetworkConfig>("network") ??
                                throw new Exception("No 'network' section in config file");

            metricsService.Start();
            networkManager.Start();
            transactionVerifier.Start();
            commandManager.Start(wallet.EcdsaKeyPair);

            // pending transactions are restored from pool repository to in-memory storage
            // it's important to restore pool after transactionVerifier and before blockSynchronizer starts
            transactionPool.Restore();
            transactionPool.StartSync();

            blockSynchronizer.Start();
            Logger.LogInformation("Synchronizing blocks...");
            blockSynchronizer.SynchronizeWith(
                validatorManager.GetValidatorsPublicKeys((long) blockManager.GetHeight())
                    .Where(key => !key.Equals(wallet.EcdsaKeyPair.PublicKey))
            );
            Logger.LogInformation("Block synchronization finished, starting consensus...");
            consensusManager.Start(blockManager.GetHeight() + 1);
            validatorStatusManager.Start(false);

            System.Console.CancelKeyPress += (sender, e) =>
            {
                System.Console.WriteLine("Interrupt received. Exiting...");
                _interrupt = true;
                Dispose();
            };

            while (!_interrupt)
                Thread.Sleep(1000);
        }
        private bool _interrupt;

        public void Dispose()
        {
            _container.Dispose();
        }
    }
}
