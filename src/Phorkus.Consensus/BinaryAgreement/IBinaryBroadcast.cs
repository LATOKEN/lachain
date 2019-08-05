using Phorkus.Utility.Utils;

namespace Phorkus.Consensus.BinaryAgreement
{
    public interface IBinaryBroadcast : IConsensusProtocol
    {
        void Input(bool value);
        
        bool Terminated(out BoolSet values);
    }
}