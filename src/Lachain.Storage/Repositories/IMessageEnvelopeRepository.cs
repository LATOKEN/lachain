using System.Collections.Generic;

namespace Lachain.Storage.Repositories
{
    public interface IMessageEnvelopeRepository
    {
        void SaveMessages(byte[] keygenState);
        byte[]? LoadMessages();        
    }
}