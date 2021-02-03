using System.Linq;
using Google.Protobuf;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Storage.State
{
    public class EventSnapshot : IEventSnapshot
    {
        private readonly IStorageState _state;

        public EventSnapshot(IStorageState state)
        {
            _state = state;
        }

        // public ulong Version => _state.CurrentVersion;
        public ulong Version
        {
            get
            {
                return _state.CurrentVersion;
            }
            set
            {
                _state.CurrentVersion = value;
            }
        }

        public void Commit()
        {
            _state.Commit();
        }

        public UInt256 Hash => _state.Hash;

        public void AddEvent(Event @event)
        {
            var total = GetTotalTransactionEvents(@event.TransactionHash ?? UInt256Utils.Zero);
            @event.Index = total;
            _state.AddOrUpdate(
                EntryPrefix.EventCountByTransactionHash.BuildPrefix(@event.TransactionHash ?? UInt256Utils.Zero),
                (total + 1).ToBytes().ToArray()
            );
            var prefix = EntryPrefix.EventByTransactionHashAndIndex.BuildPrefix(@event.TransactionHash?? UInt256Utils.Zero, total);
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
            return raw?.AsReadOnlySpan().ToUInt32() ?? 0u;
        }
    }
}