using System.Collections.Generic;

namespace Lachain.Storage.Repositories
{
    public interface IMessageEnvelopeRepository
    {
        void SaveMessages(List<byte[]> messageEnvelopeListBytes);
        void AddMessage(byte[] messageEnvelopeBytes);
        List<byte[]> LoadMessages();

        ulong GetEra();
        void SetEra(ulong era);
    }
}