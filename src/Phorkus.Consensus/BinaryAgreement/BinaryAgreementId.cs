using System;
using System.Linq;

namespace Phorkus.Consensus.BinaryAgreement
{
    public class BinaryAgreementId : IProtocolIdentifier
    {
        public BinaryAgreementId(uint epoch, uint agreementId)
        {
            Epoch = epoch;
            AgreementId = agreementId;
        }

        public uint Epoch { get; }
        public uint AgreementId { get; }
        
        public byte[] ToByteArray()
        {
            return BitConverter.GetBytes(Epoch).Concat(BitConverter.GetBytes(AgreementId)).ToArray();
        }
    }
}