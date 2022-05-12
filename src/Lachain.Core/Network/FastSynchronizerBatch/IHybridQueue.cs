using Lachain.Proto;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public interface IHybridQueue
    {
        void Initialize();
        void Add(UInt256 key);
        bool TryGetValue(out UInt256? key);
        bool ReceivedNode(UInt256 key);
        bool Complete();
        bool isPending(UInt256 key);
    }
}