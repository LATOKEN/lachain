using Lachain.Crypto.MCL.BLS12_381;

namespace Lachain.Crypto.ThresholdSignature
{
    public class PublicKey
    {
        public G1 RawKey { get; }

        public PublicKey(G1 pubKey)
        {
            RawKey = pubKey;
        }

        public bool ValidateSignature(Signature signature, byte[] message)
        {
            var mappedMessage = new G2();
            mappedMessage.SetHashOf(message);
            return Mcl.Pairing(RawKey, mappedMessage).Equals(Mcl.Pairing(G1.Generator, signature.RawSignature));
        }

        public byte[] ToByteArray()
        {
            return G1.ToBytes(RawKey);
        }

        public static PublicKey FromBytes(byte[] buffer)
        {
            return new PublicKey(G1.FromBytes(buffer));
        }
    }
}