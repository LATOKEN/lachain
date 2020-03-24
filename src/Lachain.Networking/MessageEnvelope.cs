using Lachain.Proto;

namespace Lachain.Networking
{
    public class MessageEnvelope
    {
        public MessageFactory? MessageFactory { get; set; }

        public ECDSAPublicKey? PublicKey { get; set; }
        
        public IRemotePeer? RemotePeer { get; set; }

        public Signature? Signature { get; set; }
    }
}