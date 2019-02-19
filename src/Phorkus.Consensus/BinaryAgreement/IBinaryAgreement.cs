using System;

namespace Phorkus.Consensus.BinaryAgreement
{
    public interface IBinaryAgreement : IConsensusMessageHandler
    {
        void ProposeValue(bool value);
        bool IsFinished();
        event EventHandler<bool> AgreementReached;
    }
}