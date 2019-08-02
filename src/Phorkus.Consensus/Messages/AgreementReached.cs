using Phorkus.Consensus.BinaryAgreement;

namespace Phorkus.Consensus.Messages
{
    public class AgreementReached : InternalMessage
    {
        public AgreementReached(BinaryAgreementId agreementId, bool agreedValue)
        {
            AgreedValue = agreedValue;
            AgreementId = agreementId;
        }

        public bool AgreedValue { get; }
        public BinaryAgreementId AgreementId { get; }
    }
}