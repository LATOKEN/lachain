using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lachain.Utility.Serialization;

/**
 * Message Envelopes are kept in this repository
 * Format:
 *  prefix + "0":    era
 *  prefix + "1":   count
 * prefix + "2+i": ith message
 */
namespace Lachain.Storage.Repositories
{
    public class MessageEnvelopeRepository : IMessageEnvelopeRepository
    {
        private readonly IRocksDbContext _rocksDbContext;

        public MessageEnvelopeRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }
        
        public void SaveMessages(List<byte[]> messageEnvelopeListBytes)
        {
            foreach (var envelope in messageEnvelopeListBytes)
            {
                AddMessage(envelope);
            }
        }

        public void AddMessage(byte[] messageEnvelopeBytes)
        {
            var count = GetCount();
            var key = EntryPrefix.MessageEnvelope.BuildPrefix((2+count).ToBytes());
            _rocksDbContext.Save(key, messageEnvelopeBytes);
            SetCount(count+1);
        }
        
        List<byte[]> IMessageEnvelopeRepository.LoadMessages()
        {
            throw new NotImplementedException();
        }

        public ulong GetEra()
        {
            var key = EntryPrefix.MessageEnvelope.BuildPrefix(0.ToBytes());
            return _rocksDbContext.Get(key).AsReadOnlySpan().ToUInt64();
        }

        public void SetEra(ulong era)
        {
            var key = EntryPrefix.MessageEnvelope.BuildPrefix(0.ToBytes());
            _rocksDbContext.Save(key, era.ToBytes());
        }
        
        
        public int GetCount()
        {
            var key = EntryPrefix.MessageEnvelope.BuildPrefix(1.ToBytes());
            return _rocksDbContext.Get(key).AsReadOnlySpan().ToInt32();
        }

        public void SetCount(int count)
        {
            var key = EntryPrefix.MessageEnvelope.BuildPrefix(1.ToBytes());
            _rocksDbContext.Save(key, count.ToBytes());
        }
    }
}