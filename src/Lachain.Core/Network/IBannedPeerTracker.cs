using System;
using Lachain.Proto;

namespace Lachain.Core.Network
{
    public interface IBannedPeerTracker : IDisposable
    {
        uint ThresholdForBan { get; }
        void Start();
        TransactionReceipt MakeBanRequestTransaction(ulong penalties, byte[] publicKey);
        void Stop();
    }
}