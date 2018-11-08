using NeoSharp.Core.Cryptography;

namespace NeoSharp.Core.Consensus
{
    public class ObservedValidatorState
    {
        public byte ExpectedViewNumber;
        public byte[] BlockSignature;
        public PublicKey PublicKey;
    }
}