using System;
using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Storage.Repositories
{
    public class RootProtocolMessageRepository : IMessageEnvelopeRepository
    {
        private readonly IRocksDbContext _rocksDbContext;

        public RootProtocolMessageRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }

        public void SaveMessages(byte[] messages)
        {
            var key = EntryPrefix.MessageEnvelope.BuildPrefix();
            _rocksDbContext.Save(key, messages);
        }

        public byte[] LoadMessages()
        {   
            var key = EntryPrefix.MessageEnvelope.BuildPrefix();
            return _rocksDbContext.Get(key);
        }
    }
}