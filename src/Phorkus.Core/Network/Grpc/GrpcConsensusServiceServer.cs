using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Phorkus.Core.Consensus;
using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.Network.Grpc
{
    using static Phorkus.Network.Grpc.ConsensusService;
    
    public class GrpcConsensusServiceServer : ConsensusServiceBase
    {
        private readonly IConsensusManager _consensusManager;
        private readonly ICrypto _crypto;

        public GrpcConsensusServiceServer(IConsensusManager consensusManager, ICrypto crypto)
        {
            _consensusManager = consensusManager;
            _crypto = crypto;
        }

        public override Task<BlockPrepareReply> PrepareBlock(BlockPrepareRequest request, ServerCallContext context)
        {
            if (!_VerifyMessageSignature(request, context, request.Validator))
                return Task.FromResult<BlockPrepareReply>(null);
            
            throw new NotImplementedException();
        }

        public override Task<ChangeViewReply> ChangeView(ChangeViewRequest request, ServerCallContext context)
        {
            if (!_VerifyMessageSignature(request, context, request.Validator))
                return Task.FromResult<ChangeViewReply>(null);
            
            throw new NotImplementedException();
        }

        private bool _VerifyMessageSignature(IMessage message, ServerCallContext context, Validator validator)
        {
            if (!_TryResolveAuthHeader(context, out var signature))
                return false;
            /* TODO: "maybe we also have to check validator index?" */
            return _crypto.VerifySignature(message.ToByteArray(), signature);
        }
        
        private bool _TryResolveAuthHeader(ServerCallContext context, out byte[] signature)
        {
            const string headerName = "X-Signature";
            foreach (var entry in context.RequestHeaders)
            {
                if (!entry.Key.Equals(headerName, StringComparison.OrdinalIgnoreCase))
                    continue;
                signature = entry.ValueBytes;
                return true;
            }
            signature = null;
            return false;
        }
    }
}