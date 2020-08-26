using Lachain.Networking.Hub;
using Lachain.Proto;

namespace Lachain.Networking
{
    public class MessageEnvelope
    {
        public MessageEnvelope(ECDSAPublicKey publicKey, ClientWorker remotePeer)
        {
            PublicKey = publicKey;
            RemotePeer = remotePeer;
        }
        
        public ECDSAPublicKey PublicKey { get; }

        public ClientWorker RemotePeer { get; }
    }
}