using System.IO;
using System.Reflection;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Storage;
using NUnit.Framework;
using Lachain.Consensus.Messages;
using Lachain.Storage.Repositories;


namespace Lachain.ConsensusTest
{
    public class MessageEnvelopeRepositoryManagerTest
    {
        private IContainer _container;
        private IRocksDbContext _dbContext;

        [SetUp]
        public void Setup()
        {
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            _container = containerBuilder.Build();
            _dbContext = _container.Resolve<IRocksDbContext>();
        }

        [TearDown]
        public void TearDown()
        {
            _container.Dispose();
            Lachain.UtilityTest.TestUtils.DeleteTestChainData();

        }

        [Test]
        public void Test_MessageEnvelopeRepositoryManager()
        {
            var repo = new MessageEnvelopeRepository(_dbContext);
            var manager = new MessageEnvelopeRepositoryManager(repo);
            Assert.AreEqual(manager.isPresent, false);
            
            manager.StartEra(23);
            Assert.AreEqual(manager.GetEra(), 23);
            Assert.AreEqual(manager.GetMessages().Count, 0);
            
            manager.AddMessage(new MessageEnvelope(TestUtils.GenerateBinaryBroadcastConsensusMessage(), 77));
            manager.AddMessage(new MessageEnvelope(TestUtils.GenerateBinaryBroadcastConsensusMessage(), 23));

            var era = manager.GetEra();
            var list = manager.GetMessages();
            
            manager = new MessageEnvelopeRepositoryManager(repo);
            Assert.AreEqual(manager.GetEra(), era);
            CollectionAssert.AreEqual(manager.GetMessages(), list);
            
        }
    }
}