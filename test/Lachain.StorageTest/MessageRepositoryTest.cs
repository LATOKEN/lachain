using System;
using System.IO;
using System.Reflection;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;
using Lachain.Proto;
using System.Collections.Generic;
using System.Linq;
using Lachain.Consensus;
using Lachain.Storage;
using Lachain.Storage.Repositories;


namespace Lachain.StorageTest
{
    public class MessageRepositoryTest
    {
        private IContainer _container;
        private IRocksDbContext _dbContext;
        private IBlockProducer _blockProducer;
        
        [SetUp]
        public void Setup()
        {
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));
            
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            
            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConsensusModule>();
            containerBuilder.RegisterModule<NetworkModule>();
            
            _container = containerBuilder.Build();
            _dbContext = _container.Resolve<IRocksDbContext>();
            _blockProducer = _container.Resolve<IBlockProducer>();
        }

        [TearDown]
        public void TearDown()
        {
            _container.Dispose();
            TestUtils.DeleteTestChainData();
        }

        [Test]
        [Repeat(10)]
        public void TestMessageRepository()
        {
            IMessageEnvelopeRepository repo = new MessageEnvelopeRepository(_dbContext);
                        
            Assert.IsNull(repo.GetEra());
            Assert.IsEmpty(repo.LoadMessages());
            
            repo.ClearMessages();
            Assert.IsNull(repo.GetEra());
            Assert.IsEmpty(repo.LoadMessages());
            
            repo.SetEra(23);
            Assert.AreEqual(repo.GetEra(), 23);
            Assert.IsEmpty(repo.LoadMessages());

            var messages = new List<byte[]>();
            for (var i = 1; i <= 5; i++)
            {
                messages.Add(TestUtils.GetRandomBytes(i));
            }

            foreach (var message in messages)
            {
                repo.AddMessage(message);
            }
            repo.SetEra(24);

            Assert.AreEqual(repo.GetEra(), 24);
            Assert.AreEqual(repo.LoadMessages().Count, messages.Count);

            repo.ClearMessages();
            Assert.AreEqual(repo.GetEra(), 24);
            Assert.IsEmpty(repo.LoadMessages());
            
            repo.AddMessages(messages);
            Assert.AreEqual(repo.GetEra(), 24);
            Assert.AreEqual(repo.LoadMessages().Count, messages.Count);

            foreach (var message in messages)
            {
                repo.AddMessage(message);

            }
            
            Assert.AreEqual(repo.GetEra(), 24);
            Assert.AreEqual(repo.LoadMessages().Count, 2*messages.Count);
            
            repo.ClearMessages();
            Assert.AreEqual(repo.GetEra(), 24);
            Assert.AreEqual(repo.LoadMessages().Count, 0);
            
            
            repo.AddMessages(messages);
            Assert.AreEqual(repo.GetEra(), 24);
            Assert.AreEqual(repo.LoadMessages().Count, messages.Count);

        }
    
    }
}