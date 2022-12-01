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
using Lachain.UtilityTest;
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
        
        
        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }

        [Test]
        public void SyncBlockRequestTest()
        {
            var snap = new BlockSnapsotProxy();
            for (int i = 0; i < 20; i++)  {
                snap.AddBlock(new Block());
            }

            var request = new SyncBlocksRequest();
            request.FromHeight = 0;
            request.ToHeight = 10;
            Assert.DoesNotThrow(() => _handler.ValidateSyncBlocksRequest(request, snap));
            
            request.FromHeight = 0;
            request.ToHeight = 30;
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncBlocksRequest(request, snap));

            
            request.FromHeight = 0;
            request.ToHeight = 1000000;
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncBlocksRequest(request, snap));
            
            request.FromHeight = 10;
            request.ToHeight = 0;
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncBlocksRequest(request, snap));
            
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

        [Test]
        public void SyncPoolReplyTest()
        {
            var reply = new SyncPoolReply();
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncPoolReply(reply));
            
            reply.Transactions.Add(new TransactionReceipt());
            Assert.DoesNotThrow(() => _handler.ValidateSyncPoolReply(reply));

            for (int i = 0; i < 10000; i++)
            {
                reply.Transactions.Add(new TransactionReceipt());                
            }
            Assert.Throws<ArgumentException>(() => _handler.ValidateSyncPoolReply(reply));
        }
    }
}