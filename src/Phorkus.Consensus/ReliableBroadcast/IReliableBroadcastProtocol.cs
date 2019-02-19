using System;

namespace Phorkus.Consensus.ReliableBroadcast
{
    public interface IReliableBroadcastProtocol
    {
        void ProvideInput(byte[] input);
        event EventHandler<byte[]> BroadcastCompleted;
    }
}