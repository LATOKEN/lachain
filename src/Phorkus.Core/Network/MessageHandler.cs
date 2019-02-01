using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Consensus;
using Phorkus.Logger;
using Phorkus.Networking;
using Phorkus.Proto;
using Phorkus.Storage.State;

namespace Phorkus.Core.Network
{
    public class MessageHandler : IMessageHandler
    {
        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly IConsensusManager _consensusManager;
        private readonly ILogger<MessageHandler> _logger;
        private readonly ITransactionPool _transactionPool;
        private readonly IStateManager _stateManager;

        public MessageHandler(
            IBlockSynchronizer blockSynchronizer,
            IConsensusManager consensusManager, 
            ILogger<MessageHandler> logger,
            ITransactionPool transactionPool,
            IStateManager stateManager)
        {
            _blockSynchronizer = blockSynchronizer;
            _consensusManager = consensusManager;
            _logger = logger;
            _transactionPool = transactionPool;
            _stateManager = stateManager;
        }

        public void PingRequest(MessageEnvelope envelope, PingRequest request)
        {
            var reply = envelope.MessageFactory.PingReply(request.Timestamp,
                _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight());
            envelope.RemotePeer.Send(reply);
        }

        public void PingReply(MessageEnvelope envelope, PingReply reply)
        {
            _blockSynchronizer.HandlePeerHasBlocks(reply.BlockHeight, envelope.RemotePeer);
        }

        public void GetBlocksByHashesRequest(MessageEnvelope envelope, GetBlocksByHashesRequest request)
        {
            var blocks = _stateManager.LastApprovedSnapshot.Blocks.GetBlocksByHashes(request.BlockHashes);
            envelope.RemotePeer.Send(envelope.MessageFactory.GetBlocksByHashesReply(blocks));
        }

        public void GetBlocksByHashesReply(MessageEnvelope envelope, GetBlocksByHashesReply reply)
        {
            var orderedBlocks = reply.Blocks.OrderBy(block => block.Header.Index).ToArray();
            foreach (var block in orderedBlocks)
                _blockSynchronizer.HandleBlockFromPeer(block, envelope.RemotePeer, TimeSpan.FromSeconds(5));
        }

        public void GetBlocksByHeightRangeRequest(MessageEnvelope envelope, GetBlocksByHeightRangeRequest request)
        {
            var blockHashes = _stateManager.LastApprovedSnapshot.Blocks
                .GetBlocksByHeightRange(request.FromHeight, request.ToHeight - request.FromHeight + 1)
                .Select(block => block.Hash);
            envelope.RemotePeer.Send(envelope.MessageFactory.GetBlocksByHeightRangeReply(blockHashes));
        }

        public void GetBlocksByHeightRangeReply(MessageEnvelope envelope, GetBlocksByHeightRangeReply reply)
        {
            envelope.RemotePeer.Send(envelope.MessageFactory.GetBlocksByHashesRequest(reply.BlockHashes));
        }

        public void GetTransactionsByHashesRequest(MessageEnvelope envelope, GetTransactionsByHashesRequest request)
        {
            var txs = new List<AcceptedTransaction>();
            foreach (var txHash in request.TransactionHashes)
            {
                var tx = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(txHash)
                     ?? _transactionPool.GetByHash(txHash);
                if (tx != null) 
                    txs.Add(tx);
            }
            envelope.RemotePeer.Send(envelope.MessageFactory.GetTransactionsByHashesReply(txs));
        }

        public void GetTransactionsByHashesReply(MessageEnvelope envelope, GetTransactionsByHashesReply reply)
        {
            _blockSynchronizer.HandleTransactionsFromPeer(reply.Transactions, envelope.RemotePeer);
        }

        public void ConsensusMessage(MessageEnvelope envelope, ConsensusMessage message)
        {
            switch (message.PayloadCase)
            {
                case Proto.ConsensusMessage.PayloadOneofCase.BlockPrepareRequest:
                    _consensusManager.OnPrepareRequestReceived(message.BlockPrepareRequest);
                    break;
                case Proto.ConsensusMessage.PayloadOneofCase.BlockPrepareReply:
                    _consensusManager.OnPrepareResponseReceived(message.BlockPrepareReply);
                    break;
                case Proto.ConsensusMessage.PayloadOneofCase.ChangeViewRequest:
                    _consensusManager.OnChangeViewReceived(message.ChangeViewRequest);
                    break;
                default:
                    _logger.LogWarning("Ignored unknown consensus payload of type " + message.PayloadCase);
                    break;
            }
        }
    }
}