using Phorkus.Proto;

namespace Phorkus.Networking
{
    public class MessageEnvelope
    {
        public MessageFactory MessageFactory { get; set; }

        public PublicKey PublicKey { get; set; }
        
        public IRemotePeer RemotePeer { get; set; }

        public Signature Signature { get; set; }
    }
}