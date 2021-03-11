using System;
using System.Linq;
using System.Threading;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Validators;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.Consensus;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Network;
using Lachain.Core.RPC;
using Lachain.Core.ValidatorStatus;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.Misc;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Networking;
using Lachain.Storage;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using NLog;

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
            if (!new[] { "Trace", "Debug", "Info", "Warn", "Error", "Fatal" }.Contains(logLevel))
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
            var rocksDbContext = _container.Resolve<IRocksDbContext>();
            var storageManager = _container.Resolve<IStorageManager>();
            var validationManager = _container.Resolve<IValidatorManager>();
            var localTransactionRepository = _container.Resolve<ILocalTransactionRepository>();

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

            networkManager.Start();
            transactionVerifier.Start();
            commandManager.Start(wallet.EcdsaKeyPair);
            rpcManager.Start();
            
            var startConsensusFlag = configManager.GetConfig<NetworkConfig>("network")?.ConsensusFlag ?? false;
            Logger.LogInformation($"_startConsensusFlag = {startConsensusFlag.ToString()}");
            
            if (startConsensusFlag)
            {
                consensusManager.Start((long) blockManager.GetHeight() + 1);
                validatorStatusManager.Start(false);
                
                blockSynchronizer.Start();
                Logger.LogInformation("Synchronizing blocks...");
                blockSynchronizer.SynchronizeWith(
                    validatorManager.GetValidatorsPublicKeys((long) blockManager.GetHeight())
                        .Where(key => !key.Equals(wallet.EcdsaKeyPair.PublicKey))
                );
                Logger.LogInformation("Block synchronization finished, starting consensus...");
            }
            else
            {
                Logger.LogInformation("Waiting...");
                Thread.Sleep(5000);
                
                Logger.LogInformation("Start FastSync...");
                blockSynchronizer.PerformFastSync();

                blockSynchronizer.Start();
                blockSynchronizer.SynchronizeWith(
                    validatorManager.GetValidatorsPublicKeys(0)
                        .Where(key => !key.Equals(wallet.EcdsaKeyPair.PublicKey))
                );
                Logger.LogInformation("Block synchronization finished, starting consensus...");
            }
            
            System.Console.CancelKeyPress += (sender, e) =>
            {
                System.Console.WriteLine("Interrupt received. Exiting...");
                _interrupt = true;
                Dispose();
            };

            while (!_interrupt)
                Thread.Sleep(1000);
        }

         private Block BuildNextBlock(IContainer container, TransactionReceipt[] receipts = null)
        {
            var _stateManager = container.Resolve<IStateManager>();
            receipts ??= new TransactionReceipt[] { };

            var merkleRoot = UInt256Utils.Zero;

            if (receipts.Any())
                merkleRoot = MerkleTree.ComputeRoot(receipts.Select(tx => tx.Hash).ToArray()) ??
                             throw new InvalidOperationException();

            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(_stateManager.LastApprovedSnapshot.Blocks
                    .GetTotalBlockHeight());
            var (header, multisig) =
                BuildHeaderAndMultisig(container, merkleRoot, predecessor, _stateManager.LastApprovedSnapshot.StateHash);

            return new Block
            {
                Header = header,
                Hash = header.Keccak(),
                Multisig = multisig,
                TransactionHashes = {receipts.Select(tx => tx.Hash)},
            };
        }

        private (BlockHeader, MultiSig) BuildHeaderAndMultisig(IContainer container, UInt256 merkleRoot, Block? predecessor,
            UInt256 stateHash)
        {
            var blockIndex = predecessor!.Header.Index + 1;
            var header = new BlockHeader
            {
                Index = blockIndex,
                PrevBlockHash = predecessor!.Hash,
                MerkleRoot = merkleRoot,
                StateHash = stateHash,
                Nonce = blockIndex
            };

            var _wallet = container.Resolve<IPrivateWallet>();
            var keyPair = _wallet.EcdsaKeyPair;
            var Crypto = CryptoProvider.GetCrypto();
            var headerSignature = Crypto.SignHashed(
                header.Keccak().ToBytes(),
                keyPair.PrivateKey.Encode()
            ).ToSignature();

            var multisig = new MultiSig
            {
                Quorum = 1,
                Validators = {_wallet.EcdsaKeyPair.PublicKey},
                Signatures =
                {
                    new MultiSig.Types.SignatureByValidator
                    {
                        Key = _wallet.EcdsaKeyPair.PublicKey,
                        Value = headerSignature,
                    }
                }
            };
            return (header, multisig);
        }

        private OperatingError ExecuteBlock(IContainer container, Block block, TransactionReceipt[] receipts = null)
        {
            var _stateManager = container.Resolve<IStateManager>();
            var _blockManager = container.Resolve<IBlockManager>();

            receipts ??= new TransactionReceipt[] { };

            var (_, _, stateHash, _) = _blockManager.Emulate(block, receipts);
            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(_stateManager.LastApprovedSnapshot.Blocks
                    .GetTotalBlockHeight());
            var (header, multisig) = BuildHeaderAndMultisig(container, block.Header.MerkleRoot, predecessor, stateHash);

            block.Header = header;
            block.Multisig = multisig;
            block.Hash = header.Keccak();

            var status = _blockManager.Execute(block, receipts, true, true);
            Logger.LogInformation($"Executed block: {block.Header.Index}");
            return status;
        }

        private bool _interrupt;

        public void Dispose()
        {
            _container.Dispose();
        }
    }
}