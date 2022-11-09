using System;
using System.Collections.Generic;
using System.IO;
using Lachain.Logger;
using Lachain.Storage.Repositories;

namespace Lachain.Consensus.Messages
{
    public class MessageEnvelopeRepositoryManager
    {
        private IMessageEnvelopeRepository _repository;
        private MessageEnvelopeList _messageEnvelopeList;
        private static readonly ILogger<MessageEnvelopeRepositoryManager> Logger = LoggerFactory.GetLoggerForClass<MessageEnvelopeRepositoryManager>();

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
            return _messageEnvelopeList.Era;
        }

        public void StartEra(long era)
        {
            if (isPresent && _messageEnvelopeList.Era == era)
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
            _messageEnvelopeList.AddMessage(message);
            SaveToDb(_messageEnvelopeList);
        }

        public ICollection<MessageEnvelope> GetMessages()
        {
            return _messageEnvelopeList.MessageList;
        }

        private void SaveToDb(MessageEnvelopeList messageEnvelopeList)
        {
            Logger.LogTrace("Saving list to db: " + messageEnvelopeList.ToByteArray());
            _repository.SaveMessages(messageEnvelopeList.ToByteArray());
        }
    }
}