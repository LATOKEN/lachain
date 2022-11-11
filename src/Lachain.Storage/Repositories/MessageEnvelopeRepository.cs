using System;

namespace Lachain.Storage.Repositories
{
    public class MessageEnvelopeRepository : IMessageEnvelopeRepository
    {
        private readonly IRocksDbContext _rocksDbContext;

        public MessageEnvelopeRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }

        public void SaveMessages(byte[] messages)
        {
            var key = EntryPrefix.MessageEnvelope.BuildPrefix();
            _rocksDbContext.Save(key, messages);
        }

        public byte[]? LoadMessages()
        {   
            var key = EntryPrefix.MessageEnvelope.BuildPrefix();
            return _rocksDbContext.Get(key);
        }
    }
}