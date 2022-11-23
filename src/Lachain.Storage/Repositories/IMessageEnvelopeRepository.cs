using System.Collections.Generic;

namespace Lachain.Storage.Repositories
{
    public interface IMessageEnvelopeRepository
    {
        void SaveMessages(byte[] messageEnvelopeListBytes);
        byte[]? LoadMessages();        
    }
}