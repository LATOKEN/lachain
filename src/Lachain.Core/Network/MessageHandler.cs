using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Lachain.Logger;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Consensus;
using Lachain.Crypto;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Utils;

namespace Lachain.Core.Network
{
    public class MessageHandler : IMessageHandler
    {
        private static readonly ILogger<MessageHandler> Logger = LoggerFactory.GetLoggerForClass<MessageHandler>();

        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly ITransactionPool _transactionPool;
        private readonly IStateManager _stateManager;
        private readonly IConsensusManager _consensusManager;
        private readonly INetworkManager _networkManager;
        private readonly ICrypto _crypto = CryptoProvider.GetCrypto();

        /*
         * TODO: message queue is a hack. We should design additional layer for storing/persisting consensus messages
         */
        private readonly IDictionary<long, List<Tuple<MessageEnvelope, ConsensusMessage>>> _queuedMessages =
            new ConcurrentDictionary<long, List<Tuple<MessageEnvelope, ConsensusMessage>>>();

        public MessageHandler(
            IBlockSynchronizer blockSynchronizer,
            ITransactionPool transactionPool,
            IStateManager stateManager,
            IConsensusManager consensusManager,
            IBlockManager blockManager,
            INetworkManager networkManager
        )
        {
            _blockSynchronizer = blockSynchronizer;
            _transactionPool = transactionPool;
            _stateManager = stateManager;
            _consensusManager = consensusManager;
            _networkManager = networkManager;
            blockManager.OnBlockPersisted += BlockManagerOnBlockPersisted;
            transactionPool.TransactionAdded += TransactionPoolOnTransactionAdded;
            _networkManager.OnPingRequest += OnPingRequest;
            _networkManager.OnPingReply += OnPingReply;
            _networkManager.OnGetBlocksByHashesRequest += OnGetBlocksByHashesRequest;
            _networkManager.OnGetBlocksByHashesReply += OnGetBlocksByHashesReply;
            _networkManager.OnGetBlocksByHeightRangeRequest += OnGetBlocksByHeightRangeRequest;
            _networkManager.OnGetBlocksByHeightRangeReply += OnGetBlocksByHeightRangeReply;
            _networkManager.OnGetTransactionsByHashesRequest += OnGetTransactionsByHashesRequest;
            _networkManager.OnGetTransactionsByHashesReply += OnGetTransactionsByHashesReply;
            _networkManager.OnConsensusMessage += OnConsensusMessage;
        }

        private void TransactionPoolOnTransactionAdded(object sender, TransactionReceipt e)
        {
            _networkManager.BroadcastLocalTransaction(e);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void BlockManagerOnBlockPersisted(object sender, Block e)
        {
            var era = (long) e.Header.Index + 1;
            if (!_queuedMessages.TryGetValue(era, out var messages)) return;
            _queuedMessages.Remove(era);
            foreach (var (envelope, message) in messages)
            {
                OnConsensusMessage(this, (envelope, message));
            }
        }

        private void OnPingRequest(object sender, (MessageEnvelope envelope, PingRequest request) @event)
        {
            var (envelope, request) = @event;
            var reply = envelope.MessageFactory?.PingReply(request.Timestamp,
                            _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()) ??
                        throw new InvalidOperationException();
            envelope.RemotePeer?.Send(reply);
        }

        private void OnPingReply(object sender, (MessageEnvelope envelope, PingReply reply) @event)
        {
            var (envelope, reply) = @event;
            _blockSynchronizer.HandlePeerHasBlocks(reply.BlockHeight,
                envelope.RemotePeer ?? throw new InvalidOperationException()
            );
        }

        private void OnGetBlocksByHashesRequest(object sender,
            (MessageEnvelope envelope, GetBlocksByHashesRequest request) @event)
        {
            var (envelope, request) = @event;
            var blocks = _stateManager.LastApprovedSnapshot.Blocks.GetBlocksByHashes(request.BlockHashes);
            envelope.RemotePeer?.Send(envelope.MessageFactory?.GetBlocksByHashesReply(blocks) ??
                                      throw new InvalidOperationException());
        }

        private void OnGetBlocksByHashesReply(object sender,
            (MessageEnvelope envelope, GetBlocksByHashesReply reply) @event)
        {
            var (envelope, reply) = @event;
            var orderedBlocks = reply.Blocks.OrderBy(block => block.Header.Index).ToArray();
            foreach (var block in orderedBlocks)
                _blockSynchronizer.HandleBlockFromPeer(block,
                    envelope.RemotePeer ?? throw new InvalidOperationException(), TimeSpan.FromSeconds(5));
        }

        private void OnGetBlocksByHeightRangeRequest(object sender,
            (MessageEnvelope envelope, GetBlocksByHeightRangeRequest request) @event)
        {
            var (envelope, request) = @event;
            var blockHashes = _stateManager.LastApprovedSnapshot.Blocks
                .GetBlocksByHeightRange(request.FromHeight, request.ToHeight - request.FromHeight + 1)
                .Select(block => block.Hash);
            envelope.RemotePeer?.Send(envelope.MessageFactory?.GetBlocksByHeightRangeReply(blockHashes) ??
                                      throw new InvalidOperationException());
        }

        private void OnGetBlocksByHeightRangeReply(object sender,
            (MessageEnvelope envelope, GetBlocksByHeightRangeReply reply) @event)
        {
            var (envelope, reply) = @event;
            envelope.RemotePeer?.Send(envelope.MessageFactory?.GetBlocksByHashesRequest(reply.BlockHashes) ??
                                      throw new InvalidOperationException());
        }

        private void OnGetTransactionsByHashesRequest(object sender,
            (MessageEnvelope envelope, GetTransactionsByHashesRequest request) @event)
        {
            var (envelope, request) = @event;
            var txs = request.TransactionHashes
                .Select(txHash => _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(txHash) ??
                                  _transactionPool.GetByHash(txHash))
                .Where(tx => tx != null)
                .Select(tx => tx!)
                .ToList();

            envelope.RemotePeer?.Send(envelope.MessageFactory?.GetTransactionsByHashesReply(txs) ??
                                      throw new InvalidOperationException());
        }

        private void OnGetTransactionsByHashesReply(object sender,
            (MessageEnvelope envelope, GetTransactionsByHashesReply reply) @event)
        {
            var (envelope, reply) = @event;
            _blockSynchronizer.HandleTransactionsFromPeer(reply.Transactions,
                envelope.RemotePeer ?? throw new InvalidOperationException()
            );
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnConsensusMessage(object sender, (MessageEnvelope envelope, ConsensusMessage message) @event)
        {
            var (envelope, message) = @event;
            try
            {
                if (envelope.Signature is null || envelope.PublicKey is null ||
                    !_crypto.VerifySignature(message.ToByteArray(), envelope.Signature.Encode(),
                        envelope.PublicKey.EncodeCompressed())
                )
                {
                    throw new UnauthorizedAccessException(
                        $"Message signed by validator {envelope.PublicKey?.ToHex()}, but signature is not correct");
                }

                _consensusManager.Dispatch(message, envelope.PublicKey);
            }
            catch (ConsensusStateNotPresentException)
            {
                _queuedMessages.ComputeIfAbsent(message.Validator.Era,
                        x => new List<Tuple<MessageEnvelope, ConsensusMessage>>())
                    .Add(new Tuple<MessageEnvelope, ConsensusMessage>(envelope, message));

                Logger.LogTrace("Skipped message too far in future...");
            }
        }
    }
}