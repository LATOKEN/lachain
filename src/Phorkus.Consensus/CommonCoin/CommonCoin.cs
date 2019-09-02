using System;
using System.Linq;
using Google.Protobuf;
using Phorkus.Consensus.CommonCoin.ThresholdSignature;
using Phorkus.Consensus.Messages;
using Phorkus.Proto;
using Signature = Phorkus.Consensus.CommonCoin.ThresholdSignature.Signature;

namespace Phorkus.Consensus.CommonCoin
{
    public class CommonCoin : AbstractProtocol
    {
        private readonly IThresholdSigner _thresholdSigner;
        private readonly PublicKeySet _publicKeySet;
        private readonly CoinId _coinId;
        private bool? _result;
        private int _requested;

        public override IProtocolIdentifier Id => _coinId;

        public CommonCoin(
//            PublicKeySet publicKeySet, PrivateKeyShare privateKeyShare,
            CoinId coinId, IWallet wallet, IConsensusBroadcaster broadcaster
        ) : base(wallet, broadcaster)
        {
            _publicKeySet = wallet.PublicKeySet ?? throw new ArgumentNullException(nameof(wallet.PublicKeySet));
            _coinId = coinId ?? throw new ArgumentNullException(nameof(coinId));
            _thresholdSigner = new ThresholdSigner(_coinId.ToByteArray(), wallet.PrivateKeyShare, _publicKeySet);
        }

        public void CheckResult()
        {
            if (_result == null) return;
            if (_requested != 1) return;
            _broadcaster.InternalResponse(new ProtocolResult<CoinId, bool>(_coinId, (bool) _result));
            _requested = 2;
        }

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

                var signatureShare = SignatureShare.FromBytes(message.Coin.SignatureShare.ToByteArray());
                if (!_thresholdSigner.AddShare(_publicKeySet[(int) message.Validator.ValidatorIndex], signatureShare,
                    out var signature))
                    return; // potential fault evidence

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
                        _requested = 1;
                        CheckResult();
                        _broadcaster.Broadcast(CreateCoinMessage(signatureShare));
                        break;
                    case ProtocolResult<CoinId, bool> _:
                        Terminated = true;
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