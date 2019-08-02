namespace Phorkus.Consensus.BinaryAgreement
{
    public interface IBinaryBroadcast : IConsensusProtocol
    {
        void Input(bool value);
    }
}