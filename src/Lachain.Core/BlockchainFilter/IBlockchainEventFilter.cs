namespace Lachain.Core.BlockchainFilter
{
    public interface IBlockchainEventFilter
    {
        ulong Create(BlockchainEvent eventType);

        bool Remove(ulong id);
        
        string[] Sync(ulong id);
    }
}