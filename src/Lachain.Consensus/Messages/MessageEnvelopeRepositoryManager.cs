using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Storage.Repositories;

namespace Lachain.Consensus.Messages
{
    public class MessageEnvelopeRepositoryManager : IMessageEnvelopeRepositoryManager
    {
        private readonly IMessageEnvelopeRepository _repository;
        private List<MessageEnvelope>? MessageEnvelopeList { get; set; }
        private ISet<MessageEnvelope>? MessageEnvelopeSet;
        
        private static readonly ILogger<MessageEnvelopeRepositoryManager> Logger = LoggerFactory.GetLoggerForClass<MessageEnvelopeRepositoryManager>();

        public bool IsPresent => !(MessageEnvelopeList is null);
        public MessageEnvelopeRepositoryManager(IMessageEnvelopeRepository repository)
        {
            _repository = repository;
        }
    
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void LoadFromDb()
        {
            if (_repository.GetEra() is null)
            {
                return;
            }
            
            MessageEnvelopeList = new List<MessageEnvelope>();
            MessageEnvelopeSet = new HashSet<MessageEnvelope>();

            var dbMsgs = _repository.LoadMessages();
            foreach (var bytes in dbMsgs)
            {
                var envelope = MessageEnvelope.FromByteArray(bytes);

                if (MessageEnvelopeSet.Contains(envelope))
                {
                    throw new InvalidOperationException("Duplicate message in repository" + envelope);
                }

                MessageEnvelopeSet.Add(envelope);
                MessageEnvelopeList.Add(envelope);
            }
        }

        public long GetEra()
        {
            if (!IsPresent)
            {
                throw new InvalidOperationException("Could not find MessageEnvelopeList in repository");
            }

            return (long) _repository.GetEra();
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void StartEra(long era, bool canBeSame = false)
        {
            if (!canBeSame && IsPresent && (long) _repository.GetEra() == era)
            {
                throw new ArgumentException($"Start Era called with same era number {era}");
            }
            
            _repository.ClearMessages();
            _repository.SetEra((ulong) era);
            MessageEnvelopeList = new List<MessageEnvelope>();
            MessageEnvelopeSet = new HashSet<MessageEnvelope>();
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool AddMessage(MessageEnvelope message)
        {
            if (!IsPresent)
            {
                throw new InvalidOperationException("Could not find MessageEnvelopeList in db");
            }
            if (!MessageEnvelopeSet.Contains(message))
            {
                MessageEnvelopeSet.Add(message);
                MessageEnvelopeList.Add(message);
                _repository.AddMessage(message.ToByteArray());
                Logger.LogTrace($"Saved {(message.External ? "external" : "internal")} message to db (era {_repository.GetEra()}), " +
                                $"type = ({message.TypeString()}), hashcode = {message.GetHashCode()}");
                return true;
            }
            else
            {
                Logger.LogTrace($"Not saving duplicate {(message.External ? "external" : "internal")} " +
                                $"message to db (era {_repository.GetEra()}), " +
                                $"type = ({message.TypeString()}), hashcode = {message.GetHashCode()}");
                return false;
            }
            
        }

        public ICollection<MessageEnvelope> GetMessages()
        {
            return MessageEnvelopeList;
        }
    }
}