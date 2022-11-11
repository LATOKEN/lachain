using System;
using System.IO;
using System.Reflection;
using Lachain.Consensus;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Storage;
using NUnit.Framework;
using Lachain.Consensus.Messages;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Consensus.RootProtocol;
using Lachain.Crypto.TPKE;
using Lachain.Storage.Repositories;


namespace Lachain.ConsensusTest
{
    public class MessageEnvelopeRepositoryManagerTest
    {
        private IContainer _container;
        private IRocksDbContext _dbContext;
        private Random _random;
        private IBlockProducer _blockProducer;
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
            _dbContext = _container.Resolve<IRocksDbContext>();
            _blockProducer = _container.Resolve<IBlockProducer>();
            
            const int seed = 12334;
            _random = new Random(seed);
        }

        [TearDown]
        public void TearDown()
        {
            _container.Dispose();
            UtilityTest.TestUtils.DeleteTestChainData();

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
            
            var request = new ProtocolRequest<ReliableBroadcastId, EncryptedShare?>(
                TestUtils.GenerateCommonSubsetId(_random), 
                TestUtils.GenerateReliableBroadcastId(_random), 
                TestUtils.GenerateEncryptedShare(_random, true));
            
            var requestMessage = new MessageEnvelope(request, 55);
            manager.AddMessage(requestMessage);

            var era = manager.GetEra();
            var list = manager.GetMessages();
            
            manager = new MessageEnvelopeRepositoryManager(repo);
            Assert.AreEqual(manager.GetEra(), era);
            CollectionAssert.AreEqual(manager.GetMessages(), list);
        }
        
        
        [Test]
        [Repeat(10)]
        public void TestRootProtocolSerialization()
        {
            var rootProtocolId = TestUtils.GenerateRootProtocolId(_random);
            Assert.AreEqual(rootProtocolId, RootProtocolId.FromByteArray(rootProtocolId.ToByteArray()));

            var request = new ProtocolRequest<RootProtocolId, IBlockProducer> 
                (TestUtils.GenerateCommonSubsetId(_random), rootProtocolId, _blockProducer);
            var recoveredRequest = ProtocolRequest<RootProtocolId, IBlockProducer>.FromByteArray(request.ToByteArray());
            
            Assert.AreEqual(recoveredRequest.From, request.From);
            Assert.AreEqual(recoveredRequest.To, request.To);
            Assert.IsNull(recoveredRequest.Input);
            
            var requestMessage = new MessageEnvelope(request, _random.Next(1, 100));
            var recoveredMessage = MessageEnvelope.FromByteArray(requestMessage.ToByteArray());
            
            Assert.AreEqual(recoveredMessage.External, requestMessage.External);
            Assert.AreEqual(recoveredMessage.ExternalMessage, requestMessage.ExternalMessage);
            Assert.AreEqual(recoveredMessage.ValidatorIndex, requestMessage.ValidatorIndex);
            Assert.AreEqual(recoveredMessage.InternalMessage.From, requestMessage.InternalMessage.From);
            Assert.AreEqual(recoveredMessage.InternalMessage.To, recoveredMessage.InternalMessage.To);
            Assert.IsInstanceOf(typeof(ProtocolRequest<RootProtocolId, IBlockProducer>), recoveredMessage.InternalMessage);
            Assert.IsNull(((ProtocolRequest<RootProtocolId, IBlockProducer>) recoveredMessage.InternalMessage).Input);
            
            var result = new ProtocolResult<RootProtocolId, object?> 
                (rootProtocolId, null);
            Assert.AreEqual(result, ProtocolResult<RootProtocolId, object?> .FromByteArray(result.ToByteArray()));
            
            var resultMessage = new MessageEnvelope(result, _random.Next(1, 100));
            Assert.AreEqual(resultMessage, MessageEnvelope.FromByteArray(resultMessage.ToByteArray()));
        }
    }
}