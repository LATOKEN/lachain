using System;
using System.Linq;

namespace Phorkus.Consensus.CommonCoin
{
    public class CoinId : IProtocolIdentifier
    {
        public CoinId(uint epoch, uint agreementId, uint tossId)
        {
            Epoch = epoch;
            AgreementId = agreementId;
            TossId = tossId;
        }

        public uint Epoch { get; }
        public uint AgreementId { get; }
        public uint TossId { get; }

        public byte[] ToByteArray()
        {
            return BitConverter.GetBytes(Epoch)
                .Concat(BitConverter.GetBytes(AgreementId))
                .Concat(BitConverter.GetBytes(TossId))
                .ToArray();
        }
    }
}