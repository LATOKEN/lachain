using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Crypto.ThresholdSignature
{
    public class PublicKeyShare : PublicKey
    {
        public PublicKeyShare(G1 pubKey) : base(pubKey)
        {
        }
        
        public new byte[] ToByteArray()
        {
            return G1.ToBytes(RawKey);
        }

        public new static PublicKeyShare FromBytes(byte[] buffer)
        {
            return new PublicKeyShare(G1.FromBytes(buffer));
        }
    }
}