using System;
using System.Threading.Tasks;
using NeoSharp.Core.Consensus;
using NeoSharp.Core.Messaging.Messages;
using NeoSharp.Core.Network;

namespace NeoSharp.Core.Messaging.Handlers
{
    public class ConsensusMessageHandler : MessageHandler<ConsensusMessage>
    {
        #region Private fields 
        private readonly IBroadcaster _broadcaster;
        private readonly IConsensusManager _consensusManager;

        #endregion

        #region Constructor 

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="broadcaster">Broadcaster</param>
        /// <param name="consensusManager">Consensus Manager</param>
        public ConsensusMessageHandler(IBroadcaster broadcaster, IConsensusManager consensusManager)
        {
            _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
            _consensusManager = consensusManager ?? throw new ArgumentNullException(nameof(consensusManager));
        }
        #endregion

        #region MessageHandler override methods
        /// <inheritdoc />
        public override bool CanHandle(Message message)
        {
            return message is ConsensusMessage;
        }

        /// <inheritdoc />
        public override Task Handle(ConsensusMessage message, IPeer sender)
        {
            _broadcaster.Broadcast(message, sender);
            // TODO: redirect message to ConsensusManager
            _consensusManager.HandleConsensusMessage(message);

            return Task.CompletedTask;
        }
        #endregion
    }
}