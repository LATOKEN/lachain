using System;
using System.Linq;

namespace Phorkus.Consensus.BinaryAgreement
{
    public class BinaryBroadcastId : IProtocolIdentifier
    {
        public BinaryBroadcastId(uint epoch, uint agreement, uint round)
        {
            Epoch = epoch;
            Agreement = agreement;
            Round = round;
        }

        public uint Epoch { get; }
        public uint Agreement { get; }
        public uint Round { get; }
        
        public byte[] ToByteArray()
        {
            return BitConverter.GetBytes(Epoch)
                .Concat(BitConverter.GetBytes(Agreement))
                .Concat(BitConverter.GetBytes(Round))
                .ToArray();
        }
    }
}