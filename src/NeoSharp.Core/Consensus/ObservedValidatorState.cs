using NeoSharp.Core.Cryptography;

namespace NeoSharp.Core.Consensus
{
    public class ObservedValidatorState
    {
        public byte ExpectedViewNumber;
        public byte[] BlockSignature;
        public PublicKey PublicKey;

        public ObservedValidatorState(PublicKey publicKey)
        {
            PublicKey = publicKey;
        }

        public void Reset()
        {
            BlockSignature = null;
        }
    }
}