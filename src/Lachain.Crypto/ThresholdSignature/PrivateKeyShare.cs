using Lachain.Crypto.MCL.BLS12_381;

namespace Lachain.Crypto.ThresholdSignature
{
    public class PrivateKeyShare
    {
        private readonly Fr _privateKey;

        public PrivateKeyShare(Fr privateKey)
        {
            _privateKey = privateKey;
        }

        public PublicKeyShare GetPublicKeyShare()
        {
            return new PublicKeyShare(G1.Generator * _privateKey);
        }

        public SignatureShare HashAndSign(byte[] message)
        {
            var mappedMessage = new G2();
            mappedMessage.SetHashOf(message);
            mappedMessage.Mul(mappedMessage, _privateKey);
            return new SignatureShare(mappedMessage);
        }

        public byte[] ToByteArray()
        {
            return Fr.ToBytes(_privateKey);
        }

        public static PrivateKeyShare FromBytes(byte[] buffer)
        {
            return new PrivateKeyShare(Fr.FromBytes(buffer));
        }
    }
}