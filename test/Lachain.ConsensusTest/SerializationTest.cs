using System;
using Google.Protobuf;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.Messages;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Consensus.RootProtocol;
using Lachain.ConsensusTest;
using Lachain.Crypto;
using Lachain.Crypto.TPKE;
using Lachain.Proto;
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
                TestSerializationAndAdd_BinaryAgreement();
                TestSerializationAndAdd_BinaryBroadcast();
                TestSerializationAndAdd_CommonCoin();
                TestSerializationAndAdd_CommonSubset();
                TestSerializationAndAdd_HoneyBadger();
                TestSerializationAndAdd_ReliableBroadcast();
                TestSerializationAndAdd_RootProtocol();
                TestUtils.AssertEqual(_messageList, MessageEnvelopeList.FromByteArray(_messageList.ToByteArray()));
            }
        }

        private void TestSerializationAndAdd_BinaryAgreement()
        {
            var binaryAgreementId = new BinaryAgreementId(random.Next(), random.Next());
            Assert.AreEqual(binaryAgreementId, BinaryAgreementId.FromByteArray(binaryAgreementId.ToByteArray()));
            
            
            // var binaryBroadcastMessage = TestUtils.GenerateBinaryBroadcastConsensusMessage();
            // Assert.AreEqual(binaryBroadcastMessage, ConsensusMessage.Parser.ParseFrom(binaryBroadcastMessage.ToByteArray()));
            //
            // var binaryBroadcastEnvelope = new MessageEnvelope(binaryBroadcastMessage, random.Next());
            // Assert.AreEqual(binaryBroadcastEnvelope, MessageEnvelope.FromByteArray(binaryBroadcastEnvelope.ToByteArray()));
            //
            // _messageList.addMessage(binaryBroadcastEnvelope);

        }
        
        
        private void TestSerializationAndAdd_BinaryBroadcast()
        {
            var binaryBroadcastId = new BinaryBroadcastId(random.Next(), random.Next(), random.Next());
            Assert.AreEqual(binaryBroadcastId, BinaryBroadcastId.FromByteArray(binaryBroadcastId.ToByteArray()));
        }
        
        private void TestSerializationAndAdd_CommonCoin()
        {
            var coinId = new CoinId(random.Next(), random.Next(), random.Next());
            Assert.AreEqual(coinId, CoinId.FromByteArray(coinId.ToByteArray()));
        }
        private void TestSerializationAndAdd_CommonSubset()
        {
            var commonSubsetId = new CommonSubsetId(random.Next());
            Assert.AreEqual(commonSubsetId, CommonSubsetId.FromByteArray(commonSubsetId.ToByteArray()));

            var share = TestSerializationAndGet_EncryptedShare();
        }
        private void TestSerializationAndAdd_HoneyBadger()
        {
            var honeyBadgerId = new HoneyBadgerId(random.Next());
            Assert.AreEqual(honeyBadgerId, HoneyBadgerId.FromByteArray(honeyBadgerId.ToByteArray()));
            
            var share = TestSerializationAndGet_IRawShare();
        }
        private void TestSerializationAndAdd_ReliableBroadcast()
        {
            var reliableBroadcastId = new ReliableBroadcastId(random.Next(), random.Next());
            Assert.AreEqual(reliableBroadcastId, ReliableBroadcastId.FromByteArray(reliableBroadcastId.ToByteArray()));
        }
        private void TestSerializationAndAdd_RootProtocol()
        {
            var rootProtocolId = new RootProtocolId(random.Next());
            Assert.AreEqual(rootProtocolId, RootProtocolId.FromByteArray(rootProtocolId.ToByteArray()));
        }

        private EncryptedShare TestSerializationAndGet_EncryptedShare()
        {
            var rnd = new byte[32];
            random.NextBytes(rnd);
            var share = new EncryptedShare(G1.Generator, rnd, G2.Generator, random.Next());
            Assert.AreEqual(share, EncryptedShare.FromByteArray(share.ToByteArray()));
            return share;
        }
        private IRawShare TestSerializationAndGet_IRawShare()
        {
            var rnd = new byte[32];
            random.NextBytes(rnd);
            var share = new RawShare(rnd, random.Next());
            Assert.AreEqual(share, RawShare.FromByteArray(share.ToByteArray()));
            return share;
        }
    }
}