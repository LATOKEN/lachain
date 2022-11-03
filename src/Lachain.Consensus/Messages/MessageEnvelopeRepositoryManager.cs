using System;
using System.Collections.Generic;
using Lachain.Storage.Repositories;

namespace Lachain.Consensus.Messages
{
    public class RootProtocolMessageRepositoryManager
    {
        private IMessageEnvelopeRepository _repository;
        private MessageEnvelopeList _messageEnvelopeList;
        
        public RootProtocolMessageRepositoryManager(IMessageEnvelopeRepository repository)
        {
            _repository = repository;
            var bytes = repository.LoadMessages();
            _messageEnvelopeList = MessageEnvelopeList.FromBytes(bytes);
        }
        
        public long GetEra()
        {
            return _messageEnvelopeList.era;
        }

        public void StartEra(long era)
        {
            if (_messageEnvelopeList.era == era)
            {
                throw new ArgumentException($"Start Era called with same era number {era}");
            }
            else
            {
                _messageEnvelopeList = new MessageEnvelopeList(era);
                SaveToDb(_messageEnvelopeList);
            }
        }

        public void AddMessage(MessageEnvelope message)
        {
            _messageEnvelopeList.addMessage(message);
            SaveToDb(_messageEnvelopeList);
        }

        public ICollection<MessageEnvelope> GetMessages()
        {
            return _messageEnvelopeList.messageList;
        }

        private void SaveToDb(MessageEnvelopeList messageEnvelopeList)
        {
            _repository.SaveMessages(messageEnvelopeList.ToBytes());
        }
    }
}