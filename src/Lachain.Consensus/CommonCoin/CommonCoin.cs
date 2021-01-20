using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Lachain.Logger;
using Lachain.Consensus.Messages;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Signature = Lachain.Crypto.ThresholdSignature.Signature;

namespace Lachain.Consensus.CommonCoin
{
    public class CommonCoin : AbstractProtocol
    {
        private readonly CoinId _coinId;
        private readonly IThresholdSigner _thresholdSigner;
        private CoinResult? _result;
        private ResultStatus _requested = ResultStatus.NotRequested;
        private static readonly ILogger<CommonCoin> Logger = LoggerFactory.GetLoggerForClass<CommonCoin>();

        public CommonCoin(
            CoinId coinId, IPublicConsensusKeySet wallet, PrivateKeyShare privateKeyShare,
            IConsensusBroadcaster broadcaster
        ) : base(wallet, coinId, broadcaster)
        {
            _coinId = coinId ?? throw new ArgumentNullException(nameof(coinId));
            _thresholdSigner = new ThresholdSigner(
                _coinId.ToBytes(), privateKeyShare, wallet.ThresholdSignaturePublicKeySet
            );
            _result = null;
        }

        private void CheckResult()
        {
            if (_result == null) return;
            if (_requested != ResultStatus.Requested) return;
            Broadcaster.InternalResponse(new ProtocolResult<CoinId, CoinResult>(_coinId, _result));
            Logger.LogTrace($"Player {GetMyId()}: made coin by {_coinId} and sent.");
            _requested = ResultStatus.Sent;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                if (message is null)
                {
                    _lastMessage = "Failed to decode external message";
                    throw new ArgumentNullException();
                }
                // These checks are somewhat redundant, but whatever
                if (message.PayloadCase != ConsensusMessage.PayloadOneofCase.Coin)
                {
                    _lastMessage = $"consensus message of type {message.PayloadCase} routed to CommonCoin protocol";
                    throw new ArgumentException(
                        $"consensus message of type {message.PayloadCase} routed to CommonCoin protocol");
                }

                if (message.Validator.Era != _coinId.Era ||
                    message.Coin.Agreement != _coinId.Agreement ||
                    message.Coin.Epoch != _coinId.Epoch)
                {
                    _lastMessage = $"era, agreement or epoch of message mismatched: message({message.Validator.Era}, " 
                                   + $"{message.Coin.Agreement}, {message.Coin.Epoch}), coin ({_coinId.Era}, "
                                   + $"{_coinId.Agreement}, {_coinId.Epoch})";
                    throw new ArgumentException("era, agreement or epoch of message mismatched");
                }

                Logger.LogTrace($"Received share from {envelope.ValidatorIndex}");
                _lastMessage = $"Received share from {envelope.ValidatorIndex}";
                var signatureShare = Signature.FromBytes(message.Coin.SignatureShare.ToByteArray());
                if (!_thresholdSigner.AddShare(envelope.ValidatorIndex, signatureShare, out var signature))
                {
                    _lastMessage = $"Faulty behaviour from player {envelope.ValidatorIndex}, {message.PrettyTypeString()}, {message.Coin.SignatureShare.ToByteArray().ToHex()}: bad signature share";
                    Logger.LogWarning($"Faulty behaviour from player {envelope.ValidatorIndex}, {message.PrettyTypeString()}, {message.Coin.SignatureShare.ToByteArray().ToHex()}: bad signature share");
                    return; // potential fault evidence
                }

                if (signature == null)
                {
                    _lastMessage = "signature == null";
                    return;
                }
                _result = new CoinResult(signature.RawSignature.ToBytes());
                CheckResult();
            }
            else
            {
                var message = envelope.InternalMessage;
                if (message is null)
                {
                    _lastMessage = "Failed to decode internal message";
                    throw new ArgumentNullException();
                }
                switch (message)
                {
                    case ProtocolRequest<CoinId, object?> _:
                        _lastMessage = "ProtocolRequest";
                        var signatureShare = _thresholdSigner.Sign();
                        _requested = ResultStatus.Requested;
                        CheckResult();
                        var msg = CreateCoinMessage(signatureShare);
                        Broadcaster.Broadcast(msg);
                        break;
                    case ProtocolResult<CoinId, CoinResult> _:
                        _lastMessage = "ProtocolResult";
                        Terminate();
                        break;
                    default:
                        _lastMessage = $"Binary broadcast protocol handles messages of type {message.GetType()}";
                        throw new InvalidOperationException(
                            $"Binary broadcast protocol handles messages of type {message.GetType()}");
                }
            }
        }

        private ConsensusMessage CreateCoinMessage(Signature share)
        {
            var shareBytes = share.ToBytes().ToArray();
            var message = new ConsensusMessage
            {
                Coin = new CommonCoinMessage
                {
                    Agreement = _coinId.Agreement,
                    Epoch = _coinId.Epoch,
                    SignatureShare = ByteString.CopyFrom(shareBytes, 0, shareBytes.Length)
                }
            };
            return new ConsensusMessage(message);
        }
    }
}