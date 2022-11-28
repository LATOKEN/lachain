using System.Collections.Generic;

namespace Lachain.Storage.Repositories
{
    public interface IMessageEnvelopeRepository
    {
        void AddMessages(List<byte[]> messageEnvelopeListBytes);
        void ClearMessages();
        void AddMessage(byte[] messageEnvelopeBytes);
        List<byte[]> LoadMessages();

        ulong? GetEra();
        void SetEra(ulong era);
    }
}