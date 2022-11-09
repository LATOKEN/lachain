using System;
using System.Collections.Generic;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.Messages;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Consensus.RootProtocol;
using Lachain.Crypto;
using Lachain.Crypto.TPKE;
using Lachain.Utility.Utils;
using MCL.BLS12_381.Net;
using NUnit.Framework;

namespace Lachain.ConsensusTest
{
    public class SerializationTest
    {
        private MessageEnvelopeList _messageList;
        private Random random; 
        [SetUp]
        public void SetUp()
        {
            var seed = 123456;
            random = new Random(seed);
            _messageList = new MessageEnvelopeList(random.Next());

        }

        [Test]
        public void Test_Serialization()
        {
            for (int i = 0; i < 100; i++)
            {
                TestSerializationAndAddToListBinaryAgreement(_messageList);
                TestSerializationAndAddToListBinaryBroadcast(_messageList);
                TestSerializationAndAddToListCommonCoin(_messageList);
                TestSerializationAndAddToListCommonSubset(_messageList);
                TestSerializationAndAddToListHoneyBadger(_messageList);
                TestSerializationAndAddToListReliableBroadcast(_messageList);
                TestSerializationAndAddToListRootProtocol(_messageList);
            }
        }

        private void TestSerializationAndAddToListBinaryAgreement(MessageEnvelopeList messageList)
        {
            var binaryAgreementId = TestUtils.GenerateBinaryAgreementId(random);
            Assert.AreEqual(binaryAgreementId, BinaryAgreementId.FromByteArray(binaryAgreementId.ToByteArray()));

            var request = new ProtocolRequest<BinaryAgreementId, bool> 
                (TestUtils.GenerateCommonSubsetId(random), binaryAgreementId, true);
            Assert.AreEqual(request, ProtocolRequest<BinaryAgreementId, bool>.FromByteArray(request.ToByteArray()));
            
            var requestMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(requestMessage, MessageEnvelope.FromByteArray(requestMessage.ToByteArray()));
            messageList.AddMessage(requestMessage);
            
            var result = new ProtocolResult<BinaryAgreementId, bool> (binaryAgreementId, true);
            Assert.AreEqual(request, ProtocolResult<BinaryAgreementId, bool>.FromByteArray(result.ToByteArray()));
            
            var resultMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(resultMessage, MessageEnvelope.FromByteArray(resultMessage.ToByteArray()));
            messageList.AddMessage(resultMessage);
        }
        
        
        private void TestSerializationAndAddToListBinaryBroadcast(MessageEnvelopeList messageList)
        {
            var binaryBroadcastId = TestUtils.GenerateBinaryBroadcastId(random);
            Assert.AreEqual(binaryBroadcastId, BinaryBroadcastId.FromByteArray(binaryBroadcastId.ToByteArray()));

            var request = new ProtocolRequest<BinaryBroadcastId, bool> 
                (TestUtils.GenerateBinaryAgreementId(random), binaryBroadcastId, true);
            Assert.AreEqual(request, ProtocolRequest<BinaryBroadcastId, bool>.FromByteArray(request.ToByteArray()));
            
            var requestMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(requestMessage, MessageEnvelope.FromByteArray(requestMessage.ToByteArray()));
            messageList.AddMessage(requestMessage);
            
            var result = new ProtocolResult<BinaryBroadcastId, bool> (binaryBroadcastId, true);
            Assert.AreEqual(request, ProtocolResult<BinaryBroadcastId, bool>.FromByteArray(result.ToByteArray()));
            
            var resultMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(resultMessage, MessageEnvelope.FromByteArray(resultMessage.ToByteArray()));
            messageList.AddMessage(resultMessage);
        }
        
        private void TestSerializationAndAddToListCommonCoin(MessageEnvelopeList messageList)
        {
            var coinId = TestUtils.GenerateCoinId(random);
            Assert.AreEqual(coinId, CoinId.FromByteArray(coinId.ToByteArray()));

            var request = new ProtocolRequest<CoinId, object?> 
                (TestUtils.GenerateBinaryAgreementId(random), coinId, null);
            Assert.AreEqual(request, ProtocolRequest<CoinId, bool>.FromByteArray(request.ToByteArray()));
            
            var requestMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(requestMessage, MessageEnvelope.FromByteArray(requestMessage.ToByteArray()));
            messageList.AddMessage(requestMessage);

            var coinResult = TestUtils.GenerateCoinResult(random);
            Assert.AreEqual(coinResult, CoinResult.FromByteArray(coinResult.ToByteArray()));
            
            var result = new ProtocolResult<CoinId, CoinResult> (coinId, coinResult);
            Assert.AreEqual(request, ProtocolResult<CoinId, bool>.FromByteArray(result.ToByteArray()));
            
            var resultMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(resultMessage, MessageEnvelope.FromByteArray(resultMessage.ToByteArray()));
            messageList.AddMessage(resultMessage);
        }
        private void TestSerializationAndAddToListCommonSubset(MessageEnvelopeList messageList)
        {
            var commonSubsetId = TestUtils.GenerateCommonSubsetId(random);
            Assert.AreEqual(commonSubsetId, CommonSubsetId.FromByteArray(commonSubsetId.ToByteArray()));

            var request = new ProtocolRequest<CommonSubsetId, EncryptedShare> 
                (TestUtils.GenerateCommonSubsetId(random), commonSubsetId, TestUtils.GenerateEncryptedShare(random));
            Assert.AreEqual(request, ProtocolRequest<CommonSubsetId, bool>.FromByteArray(request.ToByteArray()));
            
            var requestMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(requestMessage, MessageEnvelope.FromByteArray(requestMessage.ToByteArray()));
            messageList.AddMessage(requestMessage);

            var result = new ProtocolResult<CommonSubsetId, ISet<EncryptedShare>> (commonSubsetId, TestUtils.GenerateSetOfEncryptedShare(random));
            Assert.AreEqual(request, ProtocolResult<CommonSubsetId, bool>.FromByteArray(result.ToByteArray()));
            
            var resultMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(resultMessage, MessageEnvelope.FromByteArray(resultMessage.ToByteArray()));
            messageList.AddMessage(resultMessage);
        }
        private void TestSerializationAndAddToListHoneyBadger(MessageEnvelopeList messageList)
        {
            var honeyBadgerId = TestUtils.GenerateHoneyBadgerId(random);
            Assert.AreEqual(honeyBadgerId, HoneyBadgerId.FromByteArray(honeyBadgerId.ToByteArray()));

            var request = new ProtocolRequest<HoneyBadgerId, IRawShare> 
                (TestUtils.GenerateHoneyBadgerId(random), honeyBadgerId, TestUtils.GenerateIRawShare(random));
            Assert.AreEqual(request, ProtocolRequest<HoneyBadgerId, bool>.FromByteArray(request.ToByteArray()));
            
            var requestMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(requestMessage, MessageEnvelope.FromByteArray(requestMessage.ToByteArray()));
            messageList.AddMessage(requestMessage);

            var result = new ProtocolResult<HoneyBadgerId, ISet<IRawShare>> (honeyBadgerId, TestUtils.GenerateSetOfIRawShare(random));
            Assert.AreEqual(request, ProtocolResult<HoneyBadgerId, bool>.FromByteArray(result.ToByteArray()));
            
            var resultMessage = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(resultMessage, MessageEnvelope.FromByteArray(resultMessage.ToByteArray()));
            messageList.AddMessage(resultMessage);
        }
        private void TestSerializationAndAddToListReliableBroadcast(MessageEnvelopeList _messageList)
        {
            var reliableBroadcastId = new ReliableBroadcastId(random.Next(), random.Next());
            Assert.AreEqual(reliableBroadcastId, ReliableBroadcastId.FromByteArray(reliableBroadcastId.ToByteArray()));
        }
        private void TestSerializationAndAddToListRootProtocol(MessageEnvelopeList _messageList)
        {
            var rootProtocolId = new RootProtocolId(random.Next());
            Assert.AreEqual(rootProtocolId, RootProtocolId.FromByteArray(rootProtocolId.ToByteArray()));
        }
        

    }
}