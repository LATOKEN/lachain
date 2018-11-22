using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.Consensus
{
    public class ObservedValidatorState
    {
        public byte ExpectedViewNumber;
        public Signature BlockSignature;
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