using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Utility.Serialization;

/*
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
        
        public void AddMessages(List<byte[]> messageEnvelopeListBytes)
        {
            
            foreach (var envelope in messageEnvelopeListBytes)
            {
                AddMessage(envelope);
            }
        }
        
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ClearMessages()
        {
            var era = GetEra();
            if (era is null) {
                return;
            }
            var count = GetCount();
            var rocksDbAtomicWrite = new RocksDbAtomicWrite(_rocksDbContext);

            for (int i = 0; i < count; i++)
            {
                var key = EntryPrefix.MessageEnvelope.BuildPrefix(((2+i)).ToBytes());
                rocksDbAtomicWrite.Delete(key);
            }
            
            var countKey = EntryPrefix.MessageEnvelope.BuildPrefix(1.ToBytes());
            rocksDbAtomicWrite.Put(countKey, 0.ToBytes().ToArray());
            rocksDbAtomicWrite.Commit();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddMessage(byte[] messageEnvelopeBytes)
        {
            var rocksDbAtomicWrite = new RocksDbAtomicWrite(_rocksDbContext);
            var count = GetCount();
            var key = EntryPrefix.MessageEnvelope.BuildPrefix(((int)(2+count)).ToBytes());
            rocksDbAtomicWrite.Put(key, messageEnvelopeBytes);

            var countKey = EntryPrefix.MessageEnvelope.BuildPrefix(1.ToBytes());
            rocksDbAtomicWrite.Put(countKey, ((int)(count+1)).ToBytes().ToArray());
            rocksDbAtomicWrite.Commit();
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public List<byte[]>? LoadMessages()
        {
            var count = GetCount();
            var messages = new List<byte[]>((int)count);

            for (int i = 0; i < count; i++)
            {
                var key = EntryPrefix.MessageEnvelope.BuildPrefix((2+i).ToBytes());
                var message = _rocksDbContext.Get(key);
                if (message is null)
                {
                    throw new DataException("Cannot find $i$th message in repository");
                }
                messages.Add(message);
            }
            return messages;
        }

        public ulong? GetEra()
        {
            var key = EntryPrefix.MessageEnvelope.BuildPrefix(0.ToBytes());
            return _rocksDbContext.Get(key)?.AsReadOnlySpan().ToUInt64();
        }

        public void SetEra(ulong era)
        {
            var key = EntryPrefix.MessageEnvelope.BuildPrefix(0.ToBytes());
            _rocksDbContext.Save(key, era.ToBytes());
            
            // throw new Exception(string.Concat(era.ToBytes().Select(b => Convert.ToString(b, 2).PadLeft(8, '0'))));
        }
        
        
        private int GetCount()
        {
            var key = EntryPrefix.MessageEnvelope.BuildPrefix(1.ToBytes());
            var val = _rocksDbContext.Get(key);
            return val is null ? 0 : val.AsReadOnlySpan().ToInt32();
        }
    }
}
