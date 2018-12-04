using Phorkus.Hermes.Crypto;

namespace Phorkus.Hermes.Signer.Messages
{
    public class Round5Message : ISignerMessage
    {
        public PartialDecryption wShare;

        public Round5Message()
        {
        }
        
        public Round5Message(PartialDecryption wShare) {
            this.wShare = wShare;
        }

        public void fromByteArray(byte[] buffer)
        {
            wShare = new PartialDecryption(buffer);
        }

        public byte[] ToByteArray()
        {
            return wShare.toByteArray();
        }
    }
}