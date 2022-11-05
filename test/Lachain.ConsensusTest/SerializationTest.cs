using Google.Protobuf;
using Lachain.Consensus.Messages;
using Lachain.ConsensusTest;
using Lachain.Proto;
using NUnit.Framework;

namespace Lachain.CoreTest
{
    public class SerializationTest
    {
        [SetUp]
        public void SetUp()
        {
        }

        [Test]
        public void Test_Serialization()
        {
            var binaryBroadcastMessage = TestUtils.GenerateBinaryBroadcastConsensusMessage();
            Assert.AreEqual(binaryBroadcastMessage, ConsensusMessage.Parser.ParseFrom(binaryBroadcastMessage.ToByteArray()));

            var binaryBroadcastEnvelope = new MessageEnvelope(binaryBroadcastMessage, 2);
            Assert.AreEqual(binaryBroadcastEnvelope, MessageEnvelope.FromByteArray(binaryBroadcastEnvelope.ToByteArray()));


            var messageList = new MessageEnvelopeList(1029);
            messageList.addMessage(binaryBroadcastEnvelope);
            TestUtils.AssertEqual(messageList, MessageEnvelopeList.FromByteArray(messageList.ToByteArray()));
        }
    }
}