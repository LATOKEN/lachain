namespace Lachain.Consensus.ThresholdKeygen.Data
{
    public struct CommitMessage
    {
        public Commitment Commitment;
        public byte[][] EncryptedRows;
    }
}