using System;
using System.IO;
using System.Reflection;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Network;
using Lachain.Proto;
using NUnit.Framework;

namespace Lachain.CoreTest.Network
{
    
    public class MessageHandlerTest
    {
        private IContainer _container;
        private MessageHandler _handler;

        [SetUp]
        public void Setup()
        {
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            containerBuilder.RegisterModule<ConsensusModule>();
            containerBuilder.RegisterModule<NetworkModule>();
            _container = containerBuilder.Build();
            _handler = (MessageHandler) _container.Resolve<IMessageHandler>();
        }

        [Test]
        public void SyncBlockRequestTest()
        {
            var request = new SyncBlocksRequest();
            request.FromHeight = 0;
            request.ToHeight = 1000000;
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncBlocksRequest(request, null));
            
            request.FromHeight = 10;
            request.ToHeight = 0;
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncBlocksRequest(request, null));
            
            
            request.FromHeight = 0;
            request.ToHeight = 10;
            Assert.DoesNotThrow(() => _handler.ValidateSyncBlocksRequest(request, null));
        }
        
        [Test]
        public void SyncPoolRequestTest()
        {
            var request = new SyncPoolRequest();
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncPoolRequest(request));

            request.All = false;
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncPoolRequest(request));

            request.All = true;
            Assert.DoesNotThrow(() => _handler.ValidateSyncPoolRequest(request));

            request.All = false;
            request.Hashes.Add(new UInt256());
            Assert.DoesNotThrow(() => _handler.ValidateSyncPoolRequest(request));
            
            for (int i=0; i<10009; i++)
                request.Hashes.Add(new UInt256());
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncPoolRequest(request));
        }
        
        
    }
}