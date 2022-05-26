using System;
using Lachain.Proto;


namespace Lachain.Core.Network.FastSync
{
    public class Peer : IEquatable<Peer>
    {
        public ECDSAPublicKey _publicKey;
        public Peer(ECDSAPublicKey publicKey)
        {
            _publicKey = publicKey;
        }

        public override bool Equals(object? obj)
        {
            return _publicKey.Equals(obj);
        }

        public override int GetHashCode()
        {
            return _publicKey.GetHashCode();
        }

        public bool Equals(Peer? peer)
        {
            return !(peer is null) && _publicKey.Equals(peer._publicKey);
        }
    }
}
