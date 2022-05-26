﻿using Lachain.Core.Blockchain.Checkpoint;
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
using Lachain.Core.Network.FastSync;
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
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;

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
            var transactionPool = _container.Resolve<ITransactionPool>();
            var checkpointManager = _container.Resolve<ICheckpointManager>();

            // set chainId from config
            var chainId = configManager.GetConfig<NetworkConfig>("network")?.ChainId;
            if (chainId == null || chainId == 0) throw new Exception("chainId is not defined in the config file");

            Logger.LogInformation($"Chainid {chainId}");
            TransactionUtils.SetChainId((int)chainId);

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
            
            // set checkcpoints
            Logger.LogInformation($"Saving checkpoints.");
            var checkpointConfig = configManager.GetConfig<CheckpointConfig>("checkpoint") ??
                   throw new Exception("No checkpoint section in config file");
            checkpointManager.AddCheckpoints(checkpointConfig.AllCheckpoints);
            configManager.UpdateCheckpointConfig(checkpointManager.GetAllSavedCheckpoint());

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

            var networkConfig = configManager.GetConfig<NetworkConfig>("network") ??
                                throw new Exception("No 'network' section in config file");

            metricsService.Start();
            networkManager.Start();
            transactionVerifier.Start();
            commandManager.Start(wallet.EcdsaKeyPair);

            // pending transactions are restored from pool repository to in-memory storage
            // it's important to restore pool after transactionVerifier and before blockSynchronizer starts
            transactionPool.Restore();

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
