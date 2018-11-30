using Phorkus.Hermes.Crypto;

namespace Phorkus.Hermes.Signer.Messages
{
    public class Round6Message
    {
        public PartialDecryption sigmaShare;

        public Round6Message(PartialDecryption sigmaShare)
        {
            this.sigmaShare = sigmaShare;
        }
    }
}