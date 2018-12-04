using Phorkus.Hermes.Crypto;

namespace Phorkus.Hermes.Signer.Messages
{
    public class Round6Message : ISignerMessage
    {
        public PartialDecryption sigmaShare;

        public Round6Message()
        {
        }
        
        public Round6Message(PartialDecryption sigmaShare)
        {
            this.sigmaShare = sigmaShare;
        }

        public void fromByteArray(byte[] buffer)
        {
            sigmaShare = new PartialDecryption(buffer);
        }

        public byte[] ToByteArray()
        {
            return sigmaShare.toByteArray();
        }
    }
}