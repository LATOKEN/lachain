using System;
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
        
        
        private MessageEnvelope TestSerializationAndAddToListBinaryBroadcast(MessageEnvelopeList _messageList)
        {
            var binaryBroadcastId = new BinaryBroadcastId(random.Next(), random.Next(), random.Next());
            Assert.AreEqual(binaryBroadcastId, BinaryBroadcastId.FromByteArray(binaryBroadcastId.ToByteArray()));
            
            var bs = TestUtils.GenerateBoolSet(random);
            Assert.AreEqual(bs, BoolSet.FromByteArray(bs.ToByteArray()));
            
            
            var request = new ProtocolRequest<BinaryBroadcastId, bool> 
                (TestUtils.GenerateCommonSubsetId(random), binaryBroadcastId, true);
            
            Assert.AreEqual(request, ProtocolRequest<BinaryAgreementId, bool>.FromByteArray(request.ToByteArray()));

            var message = new MessageEnvelope(request, random.Next(1, 100));
            Assert.AreEqual(message, MessageEnvelope.FromByteArray(message.ToByteArray()));

            return message;
            
        }
        
        private void TestSerializationAndAddToListCommonCoin(MessageEnvelopeList _messageList)
        {
            var coinId = new CoinId(random.Next(), random.Next(), random.Next());
            Assert.AreEqual(coinId, CoinId.FromByteArray(coinId.ToByteArray()));

            // var cr = TestUtils.Gene(MessageEnvelopeList _messageList);
        }
        private void TestSerializationAndAddToListCommonSubset(MessageEnvelopeList _messageList)
        {
            var commonSubsetId = new CommonSubsetId(random.Next());
            Assert.AreEqual(commonSubsetId, CommonSubsetId.FromByteArray(commonSubsetId.ToByteArray()));

            var share = TestSerializationAndGetEncryptedShare();
        }
        private void TestSerializationAndAddToListHoneyBadger(MessageEnvelopeList _messageList)
        {
            var honeyBadgerId = new HoneyBadgerId(random.Next());
            Assert.AreEqual(honeyBadgerId, HoneyBadgerId.FromByteArray(honeyBadgerId.ToByteArray()));
            
            var share = TestSerializationAndGetIRawShare();
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

        private EncryptedShare TestSerializationAndGetEncryptedShare()
        {
            var rnd = new byte[32];
            random.NextBytes(rnd);
            var share = new EncryptedShare(G1.Generator, rnd, G2.Generator, random.Next());
            Assert.AreEqual(share, EncryptedShare.FromByteArray(share.ToByteArray()));
            return share;
        }
        private IRawShare TestSerializationAndGetIRawShare()
        {
            var rnd = new byte[32];
            random.NextBytes(rnd);
            var share = new RawShare(rnd, random.Next());
            Assert.AreEqual(share, RawShare.FromByteArray(share.ToByteArray()));
            return share;
        }
        
        private CoinResult TestSerializationAndGetCoinResult()
        {
            var rnd = new byte[32];
            random.NextBytes(rnd);
            var coinResult = new CoinResult(rnd);
            Assert.AreEqual(coinResult, CoinResult.FromByteArray(coinResult.ToByteArray()));
            return coinResult;
        }

        private BoolSet TestSerializationAndGetBoolSet()
        {
            bool[] bools = { random.Next(0, 1) == 1, random.Next(0, 1) == 1 };
            var bs = new BoolSet(bools);
            Assert.AreEqual(bs, BoolSet.FromByteArray(bs.ToByteArray()));
            return bs;
        }


    }
}