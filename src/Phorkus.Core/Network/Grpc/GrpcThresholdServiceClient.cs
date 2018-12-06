using Google.Protobuf;
using Grpc.Core;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Network.Grpc;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Network.Grpc
{
    public class GrpcThresholdServiceClient : IThresholdService
    {
        private readonly ThresholdService.ThresholdServiceClient _client;
        private readonly ICrypto _crypto;
        
        public GrpcThresholdServiceClient(PeerAddress peerAddress, ICrypto crypto)
        {
            _client = new ThresholdService.ThresholdServiceClient(
                new Channel(peerAddress.Host, peerAddress.Port, ChannelCredentials.Insecure));
            _crypto = crypto;
        }
        
        public ThresholdMessage ExchangeMessage(ThresholdMessage thresholdMessage, KeyPair keyPair)
        {
            return _client.ExchangeMessage(thresholdMessage, _SignMessage(thresholdMessage, keyPair));
        }
        
        private Metadata _SignMessage(IMessage message, KeyPair keyPair)
        {
            var signature = _crypto.Sign(message.ToByteArray(), keyPair.PrivateKey.Buffer.ToByteArray());
            var metadata = new Metadata
            {
                new Metadata.Entry("X-Signature", signature.ToHex())
            };
            return metadata;
        }
    }
}