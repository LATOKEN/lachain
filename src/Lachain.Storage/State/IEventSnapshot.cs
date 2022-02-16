using Lachain.Proto;
using Lachain.Utility;

namespace Lachain.Storage.State
{
    public interface IEventSnapshot : ISnapshot
    {
        void AddEvent(EventObject @eventObj);
        
        EventObject GetEventByTransactionHashAndIndex(UInt256 transactionHash, uint eventIndex);
        
        uint GetTotalTransactionEvents(UInt256 transactionHash);
    }
}