using Google.Protobuf;
using Grpc.Core;
using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.Network.Grpc
{
    using static Phorkus.Network.Grpc.ConsensusService;
    
    public class GrpcConsensusServiceClient : IConsensusService
    {
        private readonly ConsensusServiceClient _client;
        private readonly ICrypto _crypto;

        public GrpcConsensusServiceClient(PeerAddress peerAddress, ICrypto crypto)
        {
            _client = new ConsensusServiceClient(
                new Channel(peerAddress.Host, peerAddress.Port, ChannelCredentials.Insecure));
            _crypto = crypto;
        }
        
        public BlockPrepareReply PrepareBlock(BlockPrepareRequest request, KeyPair keyPair)
        {
            return _client.PrepareBlock(request, _SignMessage(request, keyPair));
        }

        public ChangeViewReply ChangeView(ChangeViewRequest request, KeyPair keyPair)
        {
            return _client.ChangeView(request, _SignMessage(request, keyPair));
        }

        private Metadata _SignMessage(IMessage message, KeyPair keyPair)
        {
            var signature = _crypto.Sign(message.ToByteArray(), keyPair.PrivateKey.Buffer.ToByteArray());
            var metadata = new Metadata
            {
                new Metadata.Entry("X-Signature", signature)
            };
            return metadata;
        }
    }
}