using System.Linq;
using Google.Protobuf;
using Lachain.Consensus;
using NUnit.Framework;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.ConsensusTest
{
    [TestFixture]
    public class ErasureCodingTest
    {
        [Test]
        public void TestErasureCoding()
        {
            const int nShards = 4, nErasures = 2;
            var rbc = new ReliableBroadcast(
                new ReliableBroadcastId(0, 0),
                new PublicConsensusKeySet(4, 1, null!, Enumerable.Empty<ECDSAPublicKey>()),
                null!
            );
            var data = Enumerable.Range(0, 100)
                .Select(x => (byte) x)
                .ToArray();
            var shards = ReliableBroadcast.ErasureCodingShards(data, nShards, nErasures);
            var echo1 = new ECHOMessage {Data = ByteString.CopyFrom(shards[1])};
            var echo2 = new ECHOMessage {Data = ByteString.CopyFrom(shards[2])};
            var decoded = rbc.DecodeFromEchos(new (ECHOMessage echo, int @from)[] {(echo1, 1), (echo2, 2)});
            Assert.IsTrue(decoded.SequenceEqual(shards.Flatten()));
        }
    }
}