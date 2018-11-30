namespace Phorkus.Hermes.Signer.Messages
{
    public class Round3Message
    {
        public byte[] riWiCommitment;
	
        public Round3Message(byte[] riWiCommitment) {
            this.riWiCommitment = riWiCommitment;
        }
    }
}