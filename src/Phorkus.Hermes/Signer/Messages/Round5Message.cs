using Phorkus.Hermes.Crypto;

namespace Phorkus.Hermes.Signer.Messages
{
    public class Round5Message
    {
        public PartialDecryption wShare;

        public Round5Message(PartialDecryption wShare) {
            this.wShare = wShare;
        }
    }
}