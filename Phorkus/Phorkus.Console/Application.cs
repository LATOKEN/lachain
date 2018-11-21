using System.Threading;
using Phorkus.Core;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.Consensus;
using Phorkus.Core.Config;
using Phorkus.Core.Cryptography;
using Phorkus.Core.DI;
using Phorkus.Core.DI.SimpleInjector;
using Phorkus.Core.Network;
using Phorkus.Core.Utils;
using Phorkus.Logger;
using Phorkus.RocksDB;

namespace Phorkus.Console
{
    public class Application : IBootstrapper
    {
        private readonly IContainer _container;
        
        public Application()
        {
            var containerBuilder = new SimpleInjectorContainerBuilder(
                new ConfigManager("config.json"));
            
            containerBuilder.RegisterModule<LoggingModule>();
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
            var networkManager = _container.Resolve<INetworkManager>();
            var blockchainManager = _container.Resolve<IBlockchainManager>();
            var configManager = _container.Resolve<IConfigManager>();
            var crypto = _container.Resolve<ICrypto>();
            
            var consensusConfig = configManager.GetConfig<ConsensusConfig>("consensus");
            var keyPair = new KeyPair(consensusConfig.PrivateKey.HexToBytes().ToPrivateKey(), crypto);
            
            System.Console.WriteLine("-------------------------------");
            System.Console.WriteLine("Private Key: " + keyPair.PrivateKey.Buffer.ToByteArray().ToHex());
            System.Console.WriteLine("Public Key: " + keyPair.PublicKey.Buffer.ToByteArray().ToHex());
            System.Console.WriteLine("Address: " + crypto.ComputeAddress(keyPair.PublicKey.Buffer.ToByteArray()).ToHex());
            System.Console.WriteLine("-------------------------------");
            
            blockchainManager.TryBuildGenesisBlock(keyPair);
            
            networkManager.Start();
            
            System.Console.CancelKeyPress += (sender, e) => _interrupt = true;
            while (!_interrupt)
                Thread.Sleep(1000);
        }
        
        private bool _interrupt;
    }
}