namespace Phorkus.Hermes.Signer.Messages
{
    public class Round1Message
    {
        public byte[] uIviCommitment;

        public Round1Message(byte[] uIviCommitment) {
            this.uIviCommitment = uIviCommitment;
        }
    }
}