using System.Linq;
using System.Threading;
using Phorkus.Core.Blockchain;
using Phorkus.Core.CLI;
using Phorkus.Core.Config;
using Phorkus.Core.Consensus;
using Phorkus.Core.DI;
using Phorkus.Core.DI.Modules;
using Phorkus.Core.DI.SimpleInjector;
using Phorkus.Core.Network;
using Phorkus.Core.RPC;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Networking;
using Phorkus.Storage;
using Phorkus.Storage.State;
using Phorkus.Utility.Utils;

namespace Phorkus.Console
{
    public class Application : IBootstrapper
    {
        private readonly IContainer _container;

        public Application()
        {
            var containerBuilder = new SimpleInjectorContainerBuilder(
                new ConfigManager("config.json"));

            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<CryptographyModule>();
            containerBuilder.RegisterModule<MessagingModule>();
            containerBuilder.RegisterModule<NetworkModule>();
            containerBuilder.RegisterModule<StorageModule>();
            
            _container = containerBuilder.Build();
        }

        public void Start(string[] args)
        {
            var blockchainManager = _container.Resolve<IBlockchainManager>();
            var blockchainContext = _container.Resolve<IBlockchainContext>();
            var configManager = _container.Resolve<IConfigManager>();
            var crypto = _container.Resolve<ICrypto>();
            var consensusManager = _container.Resolve<IConsensusManager>();
            var transactionVerifier = _container.Resolve<ITransactionVerifier>();
            var blockSynchronizer = _container.Resolve<IBlockSynchronizer>();
            var networkManager = _container.Resolve<INetworkManager>();
            var messageHandler = _container.Resolve<IMessageHandler>();
            var commandManager = _container.Resolve<IConsoleManager>();
            var rpcManager = _container.Resolve<IRpcManager>();
            var stateManager = _container.Resolve<IStateManager>();            

            var consensusConfig = configManager.GetConfig<ConsensusConfig>("consensus");
            var storageConfig = configManager.GetConfig<StorageConfig>("storage");
            
            var keyPair = new ECDSAKeyPair(consensusConfig.EcdsaPrivateKey.HexToBytes().ToPrivateKey(), crypto);
            
            System.Console.WriteLine("-------------------------------");
            System.Console.WriteLine("Private Key: " + keyPair.PrivateKey.Buffer.ToHex());
            System.Console.WriteLine("Public Key: " + keyPair.PublicKey.Buffer.ToHex());
            System.Console.WriteLine(
                "Address: " + crypto.ComputeAddress(keyPair.PublicKey.Buffer.ToArray()).ToHex());
            System.Console.WriteLine("-------------------------------");

            if (blockchainManager.TryBuildGenesisBlock())
                System.Console.WriteLine("Generated genesis block");

            var genesisBlock = stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(0);
            System.Console.WriteLine("Genesis Block: " + genesisBlock.Hash.Buffer.ToHex());
            System.Console.WriteLine($" + prevBlockHash: {genesisBlock.Header.PrevBlockHash.Buffer.ToHex()}");
            System.Console.WriteLine($" + merkleRoot: {genesisBlock.Header.MerkleRoot.Buffer.ToHex()}");
            System.Console.WriteLine($" + nonce: {genesisBlock.Header.Nonce}");
            System.Console.WriteLine($" + transactionHashes: {genesisBlock.TransactionHashes.ToArray().Length}");
            foreach (var s in genesisBlock.TransactionHashes)
                System.Console.WriteLine($" + - {s.Buffer.ToHex()}");
            System.Console.WriteLine($" + hash: {genesisBlock.Hash.Buffer.ToHex()}");
            
            var address1 = "0xe3c7a20ee19c0107b9121087bcba18eb4dcb8576".HexToUInt160();
            var address2 = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToUInt160();

            System.Console.WriteLine("-------------------------------");
            System.Console.WriteLine("Current block height: " + blockchainContext.CurrentBlockHeight);
            System.Console.WriteLine("-------------------------------");

            var networkConfig = configManager.GetConfig<NetworkConfig>("network");
            networkManager.Start(networkConfig, keyPair, messageHandler);
            transactionVerifier.Start();
            consensusManager.Start((long) blockchainContext.CurrentBlockHeight + 1);
            blockSynchronizer.Start();
            commandManager.Start(keyPair);
            rpcManager.Start();
            
            System.Console.CancelKeyPress += (sender, e) => _interrupt = true;
            
            while (!_interrupt)
                Thread.Sleep(1000);
        }
        
        private bool _interrupt;
    }
}