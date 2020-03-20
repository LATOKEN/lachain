using System;
using Google.Protobuf;
using Lachain.Proto;

namespace Lachain.Storage.State
{
    public class EventSnapshot : IEventSnapshot
    {
        private readonly IStorageState _state;

        public EventSnapshot(IStorageState state)
        {
            _state = state;
        }
        
        public ulong Version => _state.CurrentVersion;
        
        public void Commit()
        {
            _state.Commit();
        }

        public UInt256 Hash => _state.Hash;

        public void AddEvent(Event @event)
        {
            var total = GetTotalTransactionEvents(@event.TransactionHash);
            @event.Index = total;
            _state.AddOrUpdate(EntryPrefix.EventCountByTransactionHash.BuildPrefix(@event.TransactionHash),
                BitConverter.GetBytes(total + 1));
            var prefix = EntryPrefix.EventByTransactionHashAndIndex.BuildPrefix(@event.TransactionHash, total);
            _state.AddOrUpdate(prefix, @event.ToByteArray());
        }

        public Event? GetEventByTransactionHashAndIndex(UInt256 transactionHash, uint eventIndex)
        {
            var prefix = EntryPrefix.EventByTransactionHashAndIndex.BuildPrefix(transactionHash, eventIndex);
            var raw = _state.Get(prefix);
            return raw is null ? null : Event.Parser.ParseFrom(raw);
        }

        public uint GetTotalTransactionEvents(UInt256 transactionHash)
        {
            var raw = _state.Get(EntryPrefix.EventCountByTransactionHash.BuildPrefix(transactionHash));
            return raw is null ? 0 : BitConverter.ToUInt32(raw, 0);
        }
    }
}