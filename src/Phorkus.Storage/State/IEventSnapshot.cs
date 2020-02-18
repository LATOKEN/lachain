using Phorkus.Proto;

namespace Phorkus.Storage.State
{
    public interface IEventSnapshot : ISnapshot
    {
        void AddEvent(Event @event);
        
        Event? GetEventByTransactionHashAndIndex(UInt256 transactionHash, uint eventIndex);
        
        uint GetTotalTransactionEvents(UInt256 transactionHash);
    }
}