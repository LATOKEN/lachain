using System.Collections.Generic;

namespace Lachain.Consensus.Messages
{
    public interface IMessageEnvelopeRepositoryManager
    {
        bool IsPresent { get; }
        void LoadFromDb();
        long GetEra();
        void StartEra(long era, bool canBeSame = false);
        bool AddMessage(MessageEnvelope message);
        ICollection<MessageEnvelope> GetMessages();
    }
}