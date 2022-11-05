using System;
using System.Collections.Generic;
using System.IO;
using Lachain.Storage.Repositories;

namespace Lachain.Consensus.Messages
{
    public class MessageEnvelopeRepositoryManager
    {
        private IMessageEnvelopeRepository _repository;
        private MessageEnvelopeList _messageEnvelopeList;
        public bool isPresent { get; private set; }
        public MessageEnvelopeRepositoryManager(IMessageEnvelopeRepository repository)
        {
            _repository = repository;
            var bytes = repository.LoadMessages();
            isPresent = !(bytes is null);

            if (isPresent)
            {
                _messageEnvelopeList = MessageEnvelopeList.FromByteArray(bytes);
            }
                
        }
        
        public long GetEra()
        {
            if (!isPresent)
            {
                throw new InvalidOperationException("Could not find MessageEnvelopeList in db");
            }
            return _messageEnvelopeList.era;
        }

        public void StartEra(long era)
        {
            if (isPresent && _messageEnvelopeList.era == era)
            {
                throw new ArgumentException($"Start Era called with same era number {era}");
            }
     
            _messageEnvelopeList = new MessageEnvelopeList(era);
            SaveToDb(_messageEnvelopeList);
            isPresent = true;
        }

        public void AddMessage(MessageEnvelope message)
        {
            if (!isPresent)
            {
                throw new InvalidOperationException("Could not find MessageEnvelopeList in db");
            }
            _messageEnvelopeList.addMessage(message);
            SaveToDb(_messageEnvelopeList);
        }

        public ICollection<MessageEnvelope> GetMessages()
        {
            return _messageEnvelopeList.messageList;
        }

        private void SaveToDb(MessageEnvelopeList messageEnvelopeList)
        {
            _repository.SaveMessages(messageEnvelopeList.ToByteArray());
        }
    }
}