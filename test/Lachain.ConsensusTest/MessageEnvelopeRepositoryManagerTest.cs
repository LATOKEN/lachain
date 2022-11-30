using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Lachain.Consensus;
using Lachain.Consensus.CommonSubset;
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
            
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            
            containerBuilder.RegisterModule<BlockchainModule>();
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
            var manager = new MessageEnvelopeRepositoryManager(repo, 23);

            manager.LoadFromDb();
            Assert.AreEqual(manager.IsPresent, false);
            
            manager.StartEra(23);
            Assert.AreEqual(manager.IsPresent, true);
            Assert.AreEqual(manager.GetEra(), 23);
            Assert.AreEqual(manager.GetMessages().Count, 0);

            List<MessageEnvelope> messageEnvelopes = new List<MessageEnvelope>();

            var message = new MessageEnvelope(TestUtils.GenerateBinaryBroadcastConsensusMessage(), 77);
            Assert.IsTrue(manager.AddMessage(message));
            messageEnvelopes.Add(message);

            message = new MessageEnvelope(TestUtils.GenerateBinaryBroadcastConsensusMessage(), 23);
            manager.AddMessage(message);
            messageEnvelopes.Add(message);

            var request = new ProtocolRequest<ReliableBroadcastId, EncryptedShare?>(
                TestUtils.GenerateCommonSubsetId(_random), 
                TestUtils.GenerateReliableBroadcastId(_random), 
                TestUtils.GenerateEncryptedShare(_random, true));
            
            var requestMessage = new MessageEnvelope(request, 55);
            Assert.IsTrue(manager.AddMessage(requestMessage));
            messageEnvelopes.Add(requestMessage);

            Assert.IsFalse(manager.AddMessage(message));

            var era = manager.GetEra();
            var list = manager.GetMessages();
            CollectionAssert.AreEqual(list, messageEnvelopes);
            
            manager = new MessageEnvelopeRepositoryManager(repo, 24);
            manager.LoadFromDb();
            Assert.AreEqual(manager.IsPresent, true);
            Assert.AreEqual(manager.GetEra(), era);
            CollectionAssert.AreEqual(manager.GetMessages(), list);
            CollectionAssert.AreEqual(manager.GetMessages(), messageEnvelopes);

            Assert.Throws<ArgumentException>(() => manager.StartEra(23));
            
            manager.StartEra(24);
            Assert.AreEqual(manager.IsPresent, true);
            Assert.AreEqual(manager.GetEra(), 24);
            Assert.AreEqual(manager.GetMessages().Count, 0);
        }

        [Test]
        public void Test_MessageEnvelopeRepositoryManagerReloading()
        {
            var repo = new MessageEnvelopeRepository(_dbContext);
            var manager = new MessageEnvelopeRepositoryManager(repo, 45);
            
            Assert.AreEqual(manager.IsPresent, false);
            Assert.Throws<InvalidOperationException>(() => manager.GetEra());
            Assert.Throws<InvalidOperationException>(
                () => manager.AddMessage(new MessageEnvelope(TestUtils.GenerateBinaryBroadcastConsensusMessage(), 77)));

            manager.StartEra(45);
            Assert.AreEqual(manager.IsPresent, true);
            Assert.AreEqual(manager.GetEra(), 45);
            
            manager.AddMessage(new MessageEnvelope(TestUtils.GenerateBinaryBroadcastConsensusMessage(), 23));
            var list = manager.GetMessages();
            var era = manager.GetEra();
            
            manager = new MessageEnvelopeRepositoryManager(repo, era);
            Assert.AreEqual(manager.IsPresent, false);
            Assert.Throws<InvalidOperationException>(() => manager.GetEra());
            Assert.Throws<InvalidOperationException>(
                () => manager.AddMessage(new MessageEnvelope(TestUtils.GenerateBinaryBroadcastConsensusMessage(), 77)));
            
            manager.LoadFromDb();
            Assert.AreEqual(manager.IsPresent, true);
            Assert.AreEqual(manager.GetEra(), era);
            Assert.AreEqual(manager.GetMessages(), list);
        }
        
        [Test]
        [Repeat(2)]
        public void TestRootProtocolEquality()
        {
            var commonSubsetId1 = new CommonSubsetId(123);
            var commonSubsetId2 = new CommonSubsetId(123);
            Assert.AreEqual(commonSubsetId1, commonSubsetId2);
            
            var rootProtocolId1 = new RootProtocolId(123);
            var rootProtocolId2 = new RootProtocolId(123);
            Assert.AreEqual(rootProtocolId1, rootProtocolId2);

            var request1 = new ProtocolRequest<RootProtocolId, IBlockProducer> 
                (commonSubsetId1, rootProtocolId1, _blockProducer);
            
            var request2 = new ProtocolRequest<RootProtocolId, IBlockProducer> 
                (commonSubsetId2, rootProtocolId2, null);

            Assert.AreEqual(request1, request2);
            Assert.AreEqual(request1.GetHashCode(), request2.GetHashCode());

            var e1 = new MessageEnvelope(request1, 909);
            var e2 = new MessageEnvelope(request2, 909);
            Assert.AreEqual(e1, e2);
            Assert.AreEqual(e1.GetHashCode(), e2.GetHashCode());
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