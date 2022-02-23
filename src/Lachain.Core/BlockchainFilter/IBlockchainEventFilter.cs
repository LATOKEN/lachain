using Newtonsoft.Json.Linq;
using Lachain.Proto;
using System.Collections.Generic;

namespace Lachain.Core.BlockchainFilter
{
    public interface IBlockchainEventFilter
    {
        ulong Create(BlockchainEvent eventType);

        ulong Create(
            BlockchainEvent eventType, ulong? fromBlock, ulong? toBlock,  List<UInt160> addresses, List<List<UInt256>> topics
        );

        bool Remove(ulong id);
        
        JArray Sync(ulong id, bool poll);

        void RemoveUnusedFilters();
    }
}