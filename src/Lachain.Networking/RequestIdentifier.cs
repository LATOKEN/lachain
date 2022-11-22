using System;
using System.Linq;

namespace Lachain.Networking
{
    public class RequestIdentifier
    {
        public ulong RequestId { get; }
        public byte[] PeerPublicKey { get; }

        public RequestIdentifier(ulong requestId, byte[] peerPublicKey)
        {
            RequestId = requestId;
            PeerPublicKey = peerPublicKey;
        }

        protected bool Equals(RequestIdentifier other)
        {
            return RequestId == other.RequestId && PeerPublicKey.SequenceEqual(other.PeerPublicKey);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RequestIdentifier)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RequestId, PeerPublicKey.Length);
        }
    }
}