using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Consensus;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Network.Grpc
{
    using static Phorkus.Network.Grpc.ConsensusService;
    
    public class GrpcConsensusServiceServer : ConsensusServiceBase
    {
        private readonly IConsensusManager _consensusManager;
        private readonly ICrypto _crypto;
        private readonly IValidatorManager _validatorManager;

        public GrpcConsensusServiceServer(
            IConsensusManager consensusManager,
            ICrypto crypto,
            IValidatorManager validatorManager)
        {
            _consensusManager = consensusManager;
            _crypto = crypto;
            _validatorManager = validatorManager;
        }

        public override Task<BlockPrepareReply> PrepareBlock(BlockPrepareRequest request, ServerCallContext context)
        {
            if (!_VerifyMessageSignature(request, context, request.Validator))
                return Task.FromResult<BlockPrepareReply>(null);
            if (!_consensusManager.CanHandleConsensusMessage(request.Validator, request))
                return Task.FromResult(default(BlockPrepareReply));
            return Task.FromResult(_consensusManager.OnBlockPrepareReceived(request));
        }

        public override Task<ChangeViewReply> ChangeView(ChangeViewRequest request, ServerCallContext context)
        {
            if (!_VerifyMessageSignature(request, context, request.Validator))
                return Task.FromResult<ChangeViewReply>(null);
            if (!_consensusManager.CanHandleConsensusMessage(request.Validator, request))
                return Task.FromResult(default(ChangeViewReply));
            return Task.FromResult(_consensusManager.OnChangeViewReceived(request));
        }

        private bool _VerifyMessageSignature(IMessage message, ServerCallContext context, Validator validator)
        {
            if (!_TryResolveAuthHeader(context, out var signature))
                return false;
            var publicKey = _validatorManager.GetPublicKey(validator.ValidatorIndex);
            return _crypto.VerifySignature(message.ToByteArray(), signature, publicKey.Buffer.ToByteArray());
        }
        
        private bool _TryResolveAuthHeader(ServerCallContext context, out byte[] signature)
        {
            const string headerName = "X-Signature";
            foreach (var entry in context.RequestHeaders)
            {
                if (!entry.Key.Equals(headerName, StringComparison.OrdinalIgnoreCase))
                    continue;
                signature = entry.Value.HexToBytes();
                return true;
            }
            signature = null;
            return false;
        }
    }
}