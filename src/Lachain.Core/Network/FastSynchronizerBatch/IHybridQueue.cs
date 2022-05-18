using Lachain.Proto;


namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public interface IHybridQueue
    {
        void Initialize();
        // void Add(UInt256 key);
        void AddToIncomingQueue(UInt256 key);
        void AddToOutgoingQueue(UInt256 key, ulong batch);
        bool TryGetValue(out UInt256? key, out ulong? batch);
        bool ReceivedNode(UInt256 key, ulong batch);
        bool Complete();
    }
}