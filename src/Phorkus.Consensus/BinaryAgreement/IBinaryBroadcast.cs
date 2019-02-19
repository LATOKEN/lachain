using System;

namespace Phorkus.Consensus.BinaryAgreement
{
    public interface IBinaryBroadcast : IConsensusMessageHandler
    {
        void Input(int value);
        event EventHandler<int> BinValueAdded;
    }
}