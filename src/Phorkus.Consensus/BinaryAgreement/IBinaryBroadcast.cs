using System;

namespace Phorkus.Consensus.BinaryAgreement
{
    public interface IBinaryBroadcast : IConsensusProtocol
    {
        void Input(int value);
        event EventHandler<int> BinValueAdded;
    }
}