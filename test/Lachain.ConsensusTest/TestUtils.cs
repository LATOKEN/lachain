using Lachain.Consensus;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.Messages;
using Lachain.Proto;
using NUnit.Framework;

namespace Lachain.ConsensusTest
{
    public static class TestUtils
    {
        public static IPrivateConsensusKeySet EmptyWallet(int n, int f)
        {
            return new PrivateConsensusKeySet(null!, null!, null!);
        }

        public static ConsensusMessage GenerateBinaryBroadcastConsensusMessage()
        {
            var _broadcastId = new BinaryBroadcastId(2142, 42342, 13124312);
            var message = new ConsensusMessage
            {
                Bval = new BValMessage
                {
                    Agreement = _broadcastId.Agreement,
                    Epoch = _broadcastId.Epoch,
                    Value = true
                }
            };
            return message;
        }

        public static void AssertEqual(MessageEnvelopeList a, MessageEnvelopeList b)
        {
            Assert.AreEqual(a.era, b.era);
            CollectionAssert.AreEqual(a.messageList, b.messageList);
        }
    }
}