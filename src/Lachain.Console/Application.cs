using System;
using System.Linq;
using System.Threading;
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
using Lachain.Networking;
using Lachain.Storage;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using SimpleInjector;

namespace Lachain.Console
{
    public class Application : IBootstrapper, IDisposable
    {
        private readonly IContainer _container;

        public Application(string configPath, Func<string, string?, string?> argGetter)
        {
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(configPath, argGetter));
            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<MessagingModule>();
            containerBuilder.RegisterModule<NetworkModule>();
            containerBuilder.RegisterModule<StorageModule>();
            _container = containerBuilder.Build();
        }

        public void Start(string[] args)
        {
            var configManager = _container.Resolve<IConfigManager>();
            var blockManager = _container.Resolve<IBlockManager>();
            var consensusManager = _container.Resolve<IConsensusManager>();
            var validatorStatusManager = _container.Resolve<IValidatorStatusManager>();
            var transactionVerifier = _container.Resolve<ITransactionVerifier>();
            var validatorManager = _container.Resolve<IValidatorManager>();
            var blockSynchronizer = _container.Resolve<IBlockSynchronizer>();
            var networkManager = _container.Resolve<INetworkManager>();
            var messageHandler = _container.Resolve<IMessageHandler>();
            var commandManager = _container.Resolve<IConsoleManager>();
            var rpcManager = _container.Resolve<IRpcManager>();
            var stateManager = _container.Resolve<IStateManager>();
            var wallet = _container.Resolve<IPrivateWallet>();
            var localTransactionRepository = _container.Resolve<ILocalTransactionRepository>();

            localTransactionRepository.SetWatchAddress(wallet.EcdsaKeyPair.PublicKey.GetAddress());

            if (blockManager.TryBuildGenesisBlock())
                System.Console.WriteLine("Generated genesis block");

            var genesisBlock = stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(0)
                               ?? throw new Exception("Genesis block was not persisted");
            System.Console.WriteLine("Genesis Block: " + genesisBlock.Hash.ToHex());
            System.Console.WriteLine($" + prevBlockHash: {genesisBlock.Header.PrevBlockHash.ToHex()}");
            System.Console.WriteLine($" + merkleRoot: {genesisBlock.Header.MerkleRoot.ToHex()}");
            System.Console.WriteLine($" + nonce: {genesisBlock.Header.Nonce}");
            System.Console.WriteLine($" + transactionHashes: {genesisBlock.TransactionHashes.ToArray().Length}");
            foreach (var s in genesisBlock.TransactionHashes)
                System.Console.WriteLine($" + - {s.ToHex()}");
            System.Console.WriteLine($" + hash: {genesisBlock.Hash.ToHex()}");

            System.Console.WriteLine("-------------------------------");
            System.Console.WriteLine("Current block height: " + blockManager.GetHeight());
            System.Console.WriteLine($"Node public key: {wallet.EcdsaKeyPair.PublicKey.EncodeCompressed().ToHex()}");
            System.Console.WriteLine($"Node address: {wallet.EcdsaKeyPair.PublicKey.GetAddress().ToHex()}");
            System.Console.WriteLine("-------------------------------");

            var networkConfig = configManager.GetConfig<NetworkConfig>("network");

            if (!(configManager.GetCliArg("port") is null))
                networkConfig!.Port = ushort.Parse(configManager.GetCliArg("port")!);

            if (!(configManager.GetCliArg("host") is null))
                networkConfig!.MyHost = configManager.GetCliArg("host");
                
            networkManager.Start(networkConfig!, wallet.EcdsaKeyPair, messageHandler);
            transactionVerifier.Start();
            commandManager.Start(wallet.EcdsaKeyPair);
            rpcManager.Start();

            blockSynchronizer.Start();
            System.Console.WriteLine("Waiting for consensus peers to handshake...");
            networkManager.WaitForHandshake(validatorManager
                .GetValidatorsPublicKeys((long) blockManager.GetHeight())
                .Where(key => !key.Equals(wallet.EcdsaKeyPair.PublicKey))
            );
            System.Console.WriteLine("Handshake successful, synchronizing blocks...");
            blockSynchronizer.SynchronizeWith(
                validatorManager.GetValidatorsPublicKeys((long) blockManager.GetHeight())
                    .Where(key => !key.Equals(wallet.EcdsaKeyPair.PublicKey))
            );
            System.Console.WriteLine("Block synchronization finished, starting consensus...");
            consensusManager.Start((long) blockManager.GetHeight() + 1);
            validatorStatusManager.Start(false);

            System.Console.CancelKeyPress += (sender, e) =>
            {
                System.Console.WriteLine("Interrupt received. Exiting...");
                _interrupt = true;
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