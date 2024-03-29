using System;
using System.Collections.Generic;
using Lachain.Consensus;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.Messages;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Consensus.RootProtocol;
using Lachain.Core.DI;
using Lachain.Crypto;
using Lachain.Crypto.TPKE;
using Lachain.Utility.Utils;
using NUnit.Framework;

namespace Lachain.ConsensusTest
{
    public class SerializationTest
    {
        private IContainer _container;
        private List<MessageEnvelope> _messageList;
        private Random random;
        [SetUp]
        public void SetUp()
        {
            var seed = 123456;
            random = new Random(seed);
            _messageList = new List<MessageEnvelope>();
        }

        [Test]
        [Repeat(100)]
        public void Test_Serialization()
        {
            TestSerializationAndAddToListBinaryAgreement();
            TestSerializationAndAddToListBinaryBroadcast();
            TestSerializationAndAddToListCommonCoin();
            TestSerializationAndAddToListCommonSubset();
            TestSerializationAndAddToListHoneyBadger();
            TestSerializationAndAddToListReliableBroadcast();
            TestSerializationAndAddToListRootProtocol();
        }

        private void TestSerializationAndAddToListBinaryAgreement()
        {
            var binaryAgreementId = TestUtils.GenerateBinaryAgreementId(random);
            Assert.AreEqual(binaryAgreementId, BinaryAgreementId.FromByteArray(binaryAgreementId.ToByteArray()));

            var request = new ProtocolRequest<BinaryAgreementId, bool> 
                (TestUtils.GenerateCommonSubsetId(random), binaryAgreementId, true);
            Assert.AreEqual(request, ProtocolRequest<BinaryAgreementId, bool>.FromByteArray(request.ToByteArray()));
            
            var requestMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(requestMessage, MessageEnvelope.FromByteArray(requestMessage.ToByteArray()));
            
            
            var result = new ProtocolResult<BinaryAgreementId, bool> (binaryAgreementId, true);
            Assert.AreEqual(result, ProtocolResult<BinaryAgreementId, bool>.FromByteArray(result.ToByteArray()));
            
            var resultMessage = new MessageEnvelope(result, random.Next(1, 100));
            Assert.AreEqual(resultMessage, MessageEnvelope.FromByteArray(resultMessage.ToByteArray()));
            
        }
        
        
        private void TestSerializationAndAddToListBinaryBroadcast()
        {
            var binaryBroadcastId = TestUtils.GenerateBinaryBroadcastId(random);
            Assert.AreEqual(binaryBroadcastId, BinaryBroadcastId.FromByteArray(binaryBroadcastId.ToByteArray()));

            var request = new ProtocolRequest<BinaryBroadcastId, bool> 
                (TestUtils.GenerateBinaryAgreementId(random), binaryBroadcastId, true);
            Assert.AreEqual(request, ProtocolRequest<BinaryBroadcastId, bool>.FromByteArray(request.ToByteArray()));
            
            var requestMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(requestMessage, MessageEnvelope.FromByteArray(requestMessage.ToByteArray()));
            

            var bs = TestUtils.GenerateBoolSet(random);
            Assert.AreEqual(bs, BoolSet.FromByteArray(bs.ToByteArray()));
            
            var result = new ProtocolResult<BinaryBroadcastId, BoolSet> (binaryBroadcastId, bs);
            Assert.AreEqual(result, ProtocolResult<BinaryBroadcastId, BoolSet>.FromByteArray(result.ToByteArray()));
            
            var resultMessage = new MessageEnvelope(result, random.Next(1, 100));
            Assert.AreEqual(resultMessage, MessageEnvelope.FromByteArray(resultMessage.ToByteArray()));
            
        }
        
        private void TestSerializationAndAddToListCommonCoin()
        {
            var coinId = TestUtils.GenerateCoinId(random);
            Assert.AreEqual(coinId, CoinId.FromByteArray(coinId.ToByteArray()));

            var request = new ProtocolRequest<CoinId, object?> 
                (TestUtils.GenerateBinaryAgreementId(random), coinId, null);
            Assert.AreEqual(request, ProtocolRequest<CoinId, object?>.FromByteArray(request.ToByteArray()));
            
            var requestMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(requestMessage, MessageEnvelope.FromByteArray(requestMessage.ToByteArray()));
            

            var coinResult = TestUtils.GenerateCoinResult(random);
            Assert.AreEqual(coinResult, CoinResult.FromByteArray(coinResult.ToByteArray()));
            
            var result = new ProtocolResult<CoinId, CoinResult> (coinId, coinResult);
            Assert.AreEqual(result, ProtocolResult<CoinId, CoinResult>.FromByteArray(result.ToByteArray()));
            
            var resultMessage = new MessageEnvelope(result, random.Next(1, 100));
            Assert.AreEqual(resultMessage, MessageEnvelope.FromByteArray(resultMessage.ToByteArray()));
            
        }
        private void TestSerializationAndAddToListCommonSubset()
        {
            var commonSubsetId = TestUtils.GenerateCommonSubsetId(random);
            Assert.AreEqual(commonSubsetId, CommonSubsetId.FromByteArray(commonSubsetId.ToByteArray()));

            var share = TestUtils.GenerateEncryptedShare(random, false);
            Assert.AreEqual(EncryptedShare.FromByteArray(share.ToByteArray()), share);
            
            var request = new ProtocolRequest<CommonSubsetId, EncryptedShare> 
                (TestUtils.GenerateHoneyBadgerId(random), commonSubsetId, share);
            Assert.AreEqual(request, ProtocolRequest<CommonSubsetId, EncryptedShare>.FromByteArray(request.ToByteArray()));
            
            var requestMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(requestMessage, MessageEnvelope.FromByteArray(requestMessage.ToByteArray()));
            

            var result = new ProtocolResult<CommonSubsetId, ISet<EncryptedShare>> (commonSubsetId, TestUtils.GenerateSetOfEncryptedShare(random));
            Assert.AreEqual(result, ProtocolResult<CommonSubsetId, ISet<EncryptedShare>>.FromByteArray(result.ToByteArray()));
            
            var resultMessage = new MessageEnvelope(result, random.Next(1, 100));
            Assert.AreEqual(resultMessage, MessageEnvelope.FromByteArray(resultMessage.ToByteArray()));
            
        }
        private void TestSerializationAndAddToListHoneyBadger()
        {
            var honeyBadgerId = TestUtils.GenerateHoneyBadgerId(random);
            Assert.AreEqual(honeyBadgerId, HoneyBadgerId.FromByteArray(honeyBadgerId.ToByteArray()));

            var request = new ProtocolRequest<HoneyBadgerId, IRawShare> 
                (TestUtils.GenerateRootProtocolId(random), honeyBadgerId, TestUtils.GenerateIRawShare(random));
            Assert.AreEqual(request, ProtocolRequest<HoneyBadgerId, IRawShare>.FromByteArray(request.ToByteArray()));
            
            var requestMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(requestMessage, MessageEnvelope.FromByteArray(requestMessage.ToByteArray()));
            

            var result = new ProtocolResult<HoneyBadgerId, ISet<IRawShare>> (honeyBadgerId, TestUtils.GenerateSetOfIRawShare(random));
            Assert.AreEqual(result, ProtocolResult<HoneyBadgerId, ISet<IRawShare>>.FromByteArray(result.ToByteArray()));
            
            var resultMessage = new MessageEnvelope(result, random.Next(1, 100));
            Assert.AreEqual(resultMessage, MessageEnvelope.FromByteArray(resultMessage.ToByteArray()));
            
        }
        private void TestSerializationAndAddToListReliableBroadcast()
        {
            var reliableBroadcastId = TestUtils.GenerateReliableBroadcastId(random);
            Assert.AreEqual(reliableBroadcastId, ReliableBroadcastId.FromByteArray(reliableBroadcastId.ToByteArray()));

            var request = new ProtocolRequest<ReliableBroadcastId, EncryptedShare?> 
                (TestUtils.GenerateCommonSubsetId(random), reliableBroadcastId, TestUtils.GenerateEncryptedShare(random, true));
            Assert.AreEqual(request, ProtocolRequest<ReliableBroadcastId, EncryptedShare?>.FromByteArray(request.ToByteArray()));
            
            var requestMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(requestMessage, MessageEnvelope.FromByteArray(requestMessage.ToByteArray()));
            

            var result = new ProtocolResult<ReliableBroadcastId, EncryptedShare> 
                (reliableBroadcastId, TestUtils.GenerateEncryptedShare(random, false)!);
            Assert.AreEqual(result, ProtocolResult<ReliableBroadcastId, EncryptedShare> .FromByteArray(result.ToByteArray()));
            
            var resultMessage = new MessageEnvelope(result, random.Next(1, 100));
            Assert.AreEqual(resultMessage, MessageEnvelope.FromByteArray(resultMessage.ToByteArray()));
            
        }
        private void TestSerializationAndAddToListRootProtocol()
        {
            // Only checking Result as IBlockProducer will not be same in Request
            var rootProtocolId = TestUtils.GenerateRootProtocolId(random);
            Assert.AreEqual(rootProtocolId, RootProtocolId.FromByteArray(rootProtocolId.ToByteArray()));
            
            var request = new ProtocolRequest<RootProtocolId, IBlockProducer> 
                (TestUtils.GenerateCommonSubsetId(random), rootProtocolId, null);
            Assert.AreEqual(request, ProtocolRequest<RootProtocolId, IBlockProducer>.FromByteArray(request.ToByteArray()));
            
            var requestMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(requestMessage, MessageEnvelope.FromByteArray(requestMessage.ToByteArray()));
            

            var result = new ProtocolResult<RootProtocolId, object?> (rootProtocolId, null);
            Assert.AreEqual(result, ProtocolResult<RootProtocolId, object?>.FromByteArray(result.ToByteArray()));
            
            var resultMessage = new MessageEnvelope(result, random.Next(1, 100));
            Assert.AreEqual(resultMessage, MessageEnvelope.FromByteArray(resultMessage.ToByteArray()));
            
        }

        
    }
}