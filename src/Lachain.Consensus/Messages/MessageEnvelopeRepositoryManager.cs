using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Storage.Repositories;

namespace Lachain.Consensus.Messages
{
    public class MessageEnvelopeRepositoryManager
    {
        private readonly IMessageEnvelopeRepository _repository;
        private List<MessageEnvelope>? MessageEnvelopeList { get; set; }
        private ISet<MessageEnvelope>? MessageEnvelopeSet;
        private long Era { get; set; }
        
        private static readonly ILogger<MessageEnvelopeRepositoryManager> Logger = LoggerFactory.GetLoggerForClass<MessageEnvelopeRepositoryManager>();

        public bool IsPresent => !(MessageEnvelopeList is null);
        public MessageEnvelopeRepositoryManager(IMessageEnvelopeRepository repository)
        {
            _repository = repository;
        }
    
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void LoadFromDb()
        {
            Era = (long) _repository.GetEra();
            MessageEnvelopeList = new List<MessageEnvelope>();
            MessageEnvelopeSet = new HashSet<MessageEnvelope>();
            
            foreach (var bytes in _repository.LoadMessages())
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
            return Era;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void StartEra(long era, bool canBeSame = false)
        {
            if (!canBeSame && IsPresent && Era == era)
            {
                throw new ArgumentException($"Start Era called with same era number {era}");
            }
            
            _repository.ClearMessages();
            _repository.SetEra((ulong) era);
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddMessage(MessageEnvelope message)
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
                Logger.LogTrace($"Saved {(message.External ? "external" : "internal")} message to db (era {Era}), " +
                                $"type = ({message.TypeString()}), hashcode = {message.GetHashCode()}");
            }
            else
            {
                Logger.LogTrace($"Not saving duplicate {(message.External ? "external" : "internal")} " +
                                $"message to db (era {Era}), " +
                                $"type = ({message.TypeString()}), hashcode = {message.GetHashCode()}");
            }
            
        }

        public ICollection<MessageEnvelope> GetMessages()
        {
            return MessageEnvelopeList;
        }
    }
}