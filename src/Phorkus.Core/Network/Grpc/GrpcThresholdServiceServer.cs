using System;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Phorkus.Core.Threshold;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Network.Grpc;

namespace Phorkus.Core.Network.Grpc
{
    public class GrpcThresholdServiceServer : ThresholdService.ThresholdServiceBase
    {
        private readonly IThresholdManager _thresholdManager;
        private readonly ICrypto _crypto;

        public GrpcThresholdServiceServer(
            IThresholdManager thresholdManager,
            ICrypto crypto)
        {
            _thresholdManager = thresholdManager;
            _crypto = crypto;
        }
        
        public override Task<ThresholdMessage> ExchangeMessage(ThresholdMessage request, ServerCallContext context)
        {
            if (!_VerifyMessageSignature(request, context, out var publicKey))
                throw new Exception("Unable to validate ECDSA signature");
            return Task.FromResult(_thresholdManager.HandleThresholdMessage(request, publicKey.ToPublicKey()));
        }
        
        private bool _VerifyMessageSignature(IMessage message, ServerCallContext context, out byte[] publicKey)
        {
            var bytes = message.ToByteArray();
            publicKey = null;
            if (!_TryResolveAuthHeader(context, out var signature))
                return false;
            publicKey = _crypto.RecoverSignature(bytes, signature);
            return _crypto.VerifySignature(bytes, signature, publicKey);
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