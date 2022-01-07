using System.Linq;
using Google.Protobuf;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Lachain.Storage.Trie;
using System.Collections.Generic;
using Lachain.Logger;

namespace Lachain.Storage.State
{
    public class EventSnapshot : IEventSnapshot
    {

        private static readonly ILogger<EventSnapshot> Logger =
            LoggerFactory.GetLoggerForClass<EventSnapshot>();

        private readonly IStorageState _state;

        public EventSnapshot(IStorageState state)
        {
            _state = state;
        }
        public IDictionary<ulong,IHashTrieNode> GetState()
        {
            return _state.GetAllNodes();
        }

        public bool IsTrieNodeHashesOk()
        {
            return _state.IsNodeHashesOk();
        }
        
        public ulong SetState(ulong root, IDictionary<ulong, IHashTrieNode> allTrieNodes)
        {
            return _state.InsertAllNodes(root, allTrieNodes);
        }

        public ulong Version => _state.CurrentVersion;

        public void Commit()
        {
            _state.Commit();
        }

        public UInt256 Hash => _state.Hash;

        public void AddEvent(Event @event)
        {
            Logger.LogTrace($"Before transaction hash from event");
            var total = GetTotalTransactionEvents(@event.TransactionHash ?? UInt256Utils.Zero);
            Logger.LogTrace($"After transaction hash from event. total: {total}");

            @event.Index = total;

            Logger.LogTrace($"Before event-2");
            _state.AddOrUpdate(
                EntryPrefix.EventCountByTransactionHash.BuildPrefix(@event.TransactionHash ?? UInt256Utils.Zero),
                (total + 1).ToBytes().ToArray()
            );
            Logger.LogTrace("After event-2");
            Logger.LogTrace("Before event-3");
            
            var prefix = EntryPrefix.EventByTransactionHashAndIndex.BuildPrefix(@event.TransactionHash?? UInt256Utils.Zero, total);
            _state.AddOrUpdate(prefix, @event.ToByteArray());
            Logger.LogTrace("After event-3");
            
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

        public void AddToTouch(TransactionReceipt receipt)
        {
            var transactionHash = receipt.Hash;
            _state.AddToTouch(EntryPrefix.EventCountByTransactionHash.BuildPrefix(transactionHash));
            _state.AddToTouch(EntryPrefix.EventByTransactionHashAndIndex.BuildPrefix(transactionHash, 0));
        }

        public void TouchAll()
        {
            _state.TouchAll();
        }
        
        public void SetCurrentVersion(ulong root)
        {
            _state.SetCurrentVersion(root);
        }
    }
}