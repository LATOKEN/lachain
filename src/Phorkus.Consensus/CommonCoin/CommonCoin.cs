using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto.ThresholdSignature;
using Phorkus.Logger;
using Phorkus.Proto;
using Phorkus.Utility.Utils;
using Signature = Phorkus.Crypto.ThresholdSignature.Signature;

namespace Phorkus.Consensus.CommonCoin
{
    public class CommonCoin : AbstractProtocol
    {
        private readonly IThresholdSigner _thresholdSigner;
        private readonly PublicKeySet _publicKeySet;
        private readonly CoinId _coinId;
        private bool? _result;
        private ResultStatus _requested = ResultStatus.NotRequested;
        private readonly ILogger<CommonCoin> _logger = LoggerFactory.GetLoggerForClass<CommonCoin>();
        private readonly PublicKeyShare _publicKeyShare;

        public CommonCoin(
            CoinId coinId, IWallet wallet, IConsensusBroadcaster broadcaster
        ) : base(wallet, coinId, broadcaster)
        {
            _publicKeySet = wallet.ThresholdSignaturePublicKeySet ??
                            throw new ArgumentNullException(nameof(wallet.ThresholdSignaturePublicKeySet));
            _coinId = coinId ?? throw new ArgumentNullException(nameof(coinId));
            _thresholdSigner = new ThresholdSigner(_coinId.ToByteArray(), wallet.ThresholdSignaturePrivateKeyShare,
                _publicKeySet);
            _publicKeyShare = wallet.ThresholdSignaturePrivateKeyShare.GetPublicKeyShare();
        }

        public void CheckResult()
        {
            if (_result == null) return;
            if (_requested != ResultStatus.Requested) return;
            Broadcaster.InternalResponse(new ProtocolResult<CoinId, bool>(_coinId, (bool) _result));
            _logger.LogDebug($"Player {GetMyId()}: made coin by {_coinId} and sent.");
            _requested = ResultStatus.Sent;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                // These checks are somewhat redundant, but whatever
                if (message.PayloadCase != ConsensusMessage.PayloadOneofCase.Coin)
                    throw new ArgumentException(
                        $"consensus message of type {message.PayloadCase} routed to CommonCoin protocol");
                if (message.Validator.Era != _coinId.Era ||
                    message.Coin.Agreement != _coinId.Agreement ||
                    message.Coin.Epoch != _coinId.Epoch)
                    throw new ArgumentException("era, agreement or epoch of message mismatched");

                _logger.LogDebug($"received share {message.Coin.SignatureShare.ToByteArray().ToHex()} from {message.Validator.ValidatorIndex}");
                var signatureShare = SignatureShare.FromBytes(message.Coin.SignatureShare.ToByteArray());
                if (!_thresholdSigner.AddShare(_publicKeySet[(int) message.Validator.ValidatorIndex], signatureShare,
                    out var signature))
                {
                    _logger.LogDebug($"Player {GetMyId()}: faulty behaviour from player {message.Validator}");
                    return; // potential fault evidence
                }

                if (signature == null) return;
                _result = signature.Parity();
                CheckResult();
            }
            else
            {
                var message = envelope.InternalMessage;
                switch (message)
                {
                    case ProtocolRequest<CoinId, object> _:
                        var signatureShare = _thresholdSigner.Sign();
                        _logger.LogDebug($"signed payload {_coinId.ToByteArray().ToHex()} and got signature {signatureShare.ToBytes().ToHex()}");
                        _requested = ResultStatus.Requested;
                        CheckResult();
                        var msg = CreateCoinMessage(signatureShare);
                        Broadcaster.Broadcast(msg);
                        _logger.LogDebug($"sent share {msg.Coin.SignatureShare.ToByteArray().ToHex()}");
                        break;
                    case ProtocolResult<CoinId, bool> _:
                        Terminate();
                        break;
                    default:
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
                Validator = new Validator
                {
                    // TODO: somehow fill validator field
                    ValidatorIndex = GetMyId(),
                    Era = _coinId.Era
                },
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