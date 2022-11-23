using System;
using System.Runtime.CompilerServices;

namespace Lachain.Storage.Repositories
{
    public class MessageEnvelopeRepository : IMessageEnvelopeRepository
    {
        private readonly IRocksDbContext _rocksDbContext;

        public MessageEnvelopeRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SaveMessages(byte[] messageEnvelopeListBytes)
        {
            var key = EntryPrefix.MessageEnvelope.BuildPrefix();
            _rocksDbContext.Save(key, messageEnvelopeListBytes);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public byte[]? LoadMessages()
        {   
            var key = EntryPrefix.MessageEnvelope.BuildPrefix();
            return _rocksDbContext.Get(key);
        }
    }
}