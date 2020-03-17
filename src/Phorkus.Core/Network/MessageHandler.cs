using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.Pool;
using Phorkus.Core.Blockchain.Validators;
using Phorkus.Core.Consensus;
using Phorkus.Crypto;
using Phorkus.Logger;
using Phorkus.Networking;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Network
{
    public class MessageHandler : IMessageHandler
    {
        private readonly ILogger<MessageHandler> _logger = LoggerFactory.GetLoggerForClass<MessageHandler>();

        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly ITransactionPool _transactionPool;
        private readonly IStateManager _stateManager;
        private readonly IConsensusManager _consensusManager;
        private readonly IValidatorManager _validatorManager;
        private readonly ICrypto _crypto = CryptoProvider.GetCrypto();

        public MessageHandler(
            IBlockSynchronizer blockSynchronizer,
            ITransactionPool transactionPool,
            IStateManager stateManager,
            IConsensusManager consensusManager,
            IValidatorManager validatorManager
        )
        {
            _blockSynchronizer = blockSynchronizer;
            _transactionPool = transactionPool;
            _stateManager = stateManager;
            _consensusManager = consensusManager;
            _validatorManager = validatorManager;
        }

        public void PingRequest(MessageEnvelope envelope, PingRequest request)
        {
            var reply = envelope.MessageFactory?.PingReply(request.Timestamp,
                            _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()) ??
                        throw new InvalidOperationException();
            envelope.RemotePeer?.Send(reply);
        }

        public void PingReply(MessageEnvelope envelope, PingReply reply)
        {
            _blockSynchronizer.HandlePeerHasBlocks(reply.BlockHeight,
                envelope.RemotePeer ?? throw new InvalidOperationException()
            );
        }

        public void GetBlocksByHashesRequest(MessageEnvelope envelope, GetBlocksByHashesRequest request)
        {
            var blocks = _stateManager.LastApprovedSnapshot.Blocks.GetBlocksByHashes(request.BlockHashes);
            envelope.RemotePeer?.Send(envelope.MessageFactory?.GetBlocksByHashesReply(blocks) ??
                                      throw new InvalidOperationException());
        }

        public void GetBlocksByHashesReply(MessageEnvelope envelope, GetBlocksByHashesReply reply)
        {
            var orderedBlocks = reply.Blocks.OrderBy(block => block.Header.Index).ToArray();
            foreach (var block in orderedBlocks)
                _blockSynchronizer.HandleBlockFromPeer(block,
                    envelope.RemotePeer ?? throw new InvalidOperationException(), TimeSpan.FromSeconds(5));
        }

        public void GetBlocksByHeightRangeRequest(MessageEnvelope envelope, GetBlocksByHeightRangeRequest request)
        {
            var blockHashes = _stateManager.LastApprovedSnapshot.Blocks
                .GetBlocksByHeightRange(request.FromHeight, request.ToHeight - request.FromHeight + 1)
                .Select(block => block.Hash);
            envelope.RemotePeer?.Send(envelope.MessageFactory?.GetBlocksByHeightRangeReply(blockHashes) ??
                                      throw new InvalidOperationException());
        }

        public void GetBlocksByHeightRangeReply(MessageEnvelope envelope, GetBlocksByHeightRangeReply reply)
        {
            envelope.RemotePeer?.Send(envelope.MessageFactory?.GetBlocksByHashesRequest(reply.BlockHashes) ??
                                      throw new InvalidOperationException());
        }

        public void GetTransactionsByHashesRequest(MessageEnvelope envelope, GetTransactionsByHashesRequest request)
        {
            var txs = request.TransactionHashes
                .Select(txHash => _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(txHash) ??
                                  _transactionPool.GetByHash(txHash))
                .Where(tx => tx != null)
                .ToList();

            envelope.RemotePeer?.Send(envelope.MessageFactory?.GetTransactionsByHashesReply(txs) ??
                                      throw new InvalidOperationException());
        }

        public void GetTransactionsByHashesReply(MessageEnvelope envelope, GetTransactionsByHashesReply reply)
        {
            _blockSynchronizer.HandleTransactionsFromPeer(reply.Transactions,
                envelope.RemotePeer ?? throw new InvalidOperationException()
            );
        }

        public void ConsensusMessage(MessageEnvelope envelope, ConsensusMessage message)
        {
            var index = _validatorManager.GetValidatorIndex(
                envelope.PublicKey ?? throw new InvalidOperationException(),
                message.Validator.Era
            );

            if (envelope.Signature is null ||
                !_crypto.VerifySignature(message.ToByteArray(), envelope.Signature.Encode(),
                    envelope.PublicKey.EncodeCompressed())
            )
            {
                throw new UnauthorizedAccessException(
                    $"Message signed by validator {index}, but signature is not correct");
            }

            _consensusManager.Dispatch(message, index);
        }
    }
}