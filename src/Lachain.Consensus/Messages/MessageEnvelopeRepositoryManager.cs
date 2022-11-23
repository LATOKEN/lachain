using System;
using System.Collections.Generic;
using Lachain.Logger;
using Lachain.Storage.Repositories;

namespace Lachain.Consensus.Messages
{
    public class MessageEnvelopeRepositoryManager
    {
        private readonly IMessageEnvelopeRepository _repository;
        private MessageEnvelopeList? _messageEnvelopeList;
        private static readonly ILogger<MessageEnvelopeRepositoryManager> logger = LoggerFactory.GetLoggerForClass<MessageEnvelopeRepositoryManager>();

        public bool IsPresent => !(_messageEnvelopeList is null);
        public MessageEnvelopeRepositoryManager(IMessageEnvelopeRepository repository)
        {
            _repository = repository;
            var bytes = repository.LoadMessages();
            _messageEnvelopeList = !(bytes is null) ? MessageEnvelopeList.FromByteArray(bytes) : null;
        }
        
        public long GetEra()
        {
            if (!IsPresent)
            {
                throw new InvalidOperationException("Could not find MessageEnvelopeList in db");
            }
            return _messageEnvelopeList.Era;
        }

        public void StartEra(long era, bool canBeSame = false)
        {
            if (!canBeSame && IsPresent && _messageEnvelopeList.Era == era)
            {
                throw new ArgumentException($"Start Era called with same era number {era}");
            }
     
            _messageEnvelopeList = new MessageEnvelopeList(era);
            SaveToDb(_messageEnvelopeList);
        }

        public void AddMessage(MessageEnvelope message)
        {
            if (!IsPresent)
            {
                throw new InvalidOperationException("Could not find MessageEnvelopeList in db");
            }

            try
            {
                _messageEnvelopeList.AddMessage(message);
                SaveToDb(_messageEnvelopeList);
                logger.LogTrace($"Saved {(message.External ? "external" : "internal")} message to db (era {_messageEnvelopeList.Era}), " +
                                $"type = ({message.TypeString()}), hashcode = {message.GetHashCode()}");
            }
            catch (ArgumentException e)
            {
                logger.LogTrace($"Not saving duplicate {(message.External ? "external" : "internal")} " +
                                $"message to db (era {_messageEnvelopeList.Era}), " +
                                $"type = ({message.TypeString()}), hashcode = {message.GetHashCode()}");
            }
            
        }

        public ICollection<MessageEnvelope> GetMessages()
        {
            return _messageEnvelopeList.MessageList;
        }
        
        private void SaveToDb(MessageEnvelopeList messageEnvelopeList)
        {
            _repository.SaveMessages(messageEnvelopeList.ToByteArray());
        }
    }
}