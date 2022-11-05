using Google.Protobuf;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.Messages;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Consensus.RootProtocol;
using Lachain.ConsensusTest;
using Lachain.Proto;
using NUnit.Framework;

namespace Lachain.CoreTest
{
    public class SerializationTest
    {
        private MessageEnvelopeList _messageList;
        [SetUp]
        public void SetUp()
        {
            _messageList = new MessageEnvelopeList(1029);
        }

        [Test]
        public void Test_Serialization()
        {
           TestBinaryAgreementSerialization();
           TestBinaryBroadcastSerialization();
           TestCommonCoinSerialization();
           TestHoneyBadgerSerialization();
           TestHoneyBadgerSerialization();
           TestReliableBroadcastSerialization();
           TestRootProtocolSerialization();

           TestUtils.AssertEqual(_messageList, MessageEnvelopeList.FromByteArray(_messageList.ToByteArray()));
        }

        private void TestBinaryAgreementSerialization()
        {
            var binaryAgreementId = new BinaryAgreementId(4944, 32232352);
            Assert.AreEqual(binaryAgreementId, BinaryAgreementId.FromByteArray(binaryAgreementId.ToByteArray()));
            
            
            // var binaryBroadcastMessage = TestUtils.GenerateBinaryBroadcastConsensusMessage();
            // Assert.AreEqual(binaryBroadcastMessage, ConsensusMessage.Parser.ParseFrom(binaryBroadcastMessage.ToByteArray()));
            //
            // var binaryBroadcastEnvelope = new MessageEnvelope(binaryBroadcastMessage, 2);
            // Assert.AreEqual(binaryBroadcastEnvelope, MessageEnvelope.FromByteArray(binaryBroadcastEnvelope.ToByteArray()));
            //
            // _messageList.addMessage(binaryBroadcastEnvelope);

        }
        
        
        private void TestBinaryBroadcastSerialization()
        {
            var binaryBroadcastId = new BinaryBroadcastId(4944, 8998452, 1234567);
            Assert.AreEqual(binaryBroadcastId, BinaryBroadcastId.FromByteArray(binaryBroadcastId.ToByteArray()));
        }
        
        private void TestCommonCoinSerialization()
        {
            var coinId = new CoinId(8744, 322322, 44123);
            Assert.AreEqual(coinId, CoinId.FromByteArray(coinId.ToByteArray()));
        }
        private void TestCommonSubsetSerialization()
        {
            var commonSubsetId = new CommonSubsetId(32352);
            Assert.AreEqual(commonSubsetId, CommonSubsetId.FromByteArray(commonSubsetId.ToByteArray()));
        }
        private void TestHoneyBadgerSerialization()
        {
            var honeyBadgerId = new HoneyBadgerId(2210);
            Assert.AreEqual(honeyBadgerId, HoneyBadgerId.FromByteArray(honeyBadgerId.ToByteArray()));
        }
        private void TestReliableBroadcastSerialization()
        {
            var reliableBroadcastId = new ReliableBroadcastId(9987, 122531);
            Assert.AreEqual(reliableBroadcastId, ReliableBroadcastId.FromByteArray(reliableBroadcastId.ToByteArray()));
        }
        private void TestRootProtocolSerialization()
        {
            var rootProtocolId = new RootProtocolId(4587);
            Assert.AreEqual(rootProtocolId, RootProtocolId.FromByteArray(rootProtocolId.ToByteArray()));
        }
    }
}