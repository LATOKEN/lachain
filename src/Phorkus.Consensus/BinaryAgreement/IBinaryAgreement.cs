using System;

namespace Phorkus.Consensus.BinaryAgreement
{
    public interface IBinaryAgreement : IConsensusProtocol
    {
        void ProposeValue(bool value);
        bool IsFinished();
        event EventHandler<bool> AgreementReached;
    }
}