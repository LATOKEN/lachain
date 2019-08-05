using System;
using System.Linq;
using Google.Protobuf;
using Phorkus.Consensus.CommonCoin.ThresholdCrypto;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;
using Signature = Phorkus.Consensus.CommonCoin.ThresholdCrypto.Signature;

namespace Phorkus.Consensus.CommonCoin
{
    public class CommonCoin : ICommonCoin
    {
        private readonly IThresholdSigner _thresholdSigner;
        private readonly PublicKeySet _publicKeySet;
        private readonly CoinId _coinId;
        private readonly IConsensusBroadcaster _broadcaster;
        private bool _terminated;
        private bool _result;

        public CommonCoin(
            PublicKeySet publicKeySet, PrivateKeyShare privateKeyShare,
            CoinId coinId, IConsensusBroadcaster broadcaster
        )
        {
            _publicKeySet = publicKeySet ?? throw new ArgumentNullException(nameof(publicKeySet));
            _coinId = coinId ?? throw new ArgumentNullException(nameof(coinId));
            _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
            _thresholdSigner = new ThresholdSigner(_coinId.ToByteArray(), privateKeyShare, publicKeySet);
        }

        public void RequestCoin()
        {
            var signatureShare = _thresholdSigner.Sign();
            _broadcaster.Broadcast(CreateCoinMessage(signatureShare));
        }

        public bool Terminated(out bool coin)
        {
            coin = _result;
            return _terminated;
        }

        public IProtocolIdentifier Id => _coinId;

        public void HandleMessage(ConsensusMessage message)
        {
            if (_terminated) return;
            // These checks are somewhat redundant, but whatever
            if (message.PayloadCase != ConsensusMessage.PayloadOneofCase.Coin)
                throw new ArgumentException(
                    $"consensus message of type {message.PayloadCase} routed to CommonCoin protocol");
            if (message.Validator.Era != _coinId.Era ||
                message.Coin.Agreement != _coinId.Agreement ||
                message.Coin.Epoch != _coinId.Epoch)
                throw new ArgumentException("era, agreement or epoch of message mismatched");

            var signatureShare = SignatureShare.FromBytes(message.Coin.SignatureShare.ToByteArray());
            if (!_thresholdSigner.AddShare(_publicKeySet[(int) message.Validator.ValidatorIndex], signatureShare, out var signature))
                return; // potential fault evidence

            if (signature == null) return;
            _broadcaster.MessageSelf(new CoinTossed(_coinId, signature.Parity()));
        }

        public void HandleInternalMessage(InternalMessage message)
        {
            switch (message)
            {
                case CoinTossed coinTossed:
                    _result = coinTossed.CoinValue;
                    _terminated = true;
                    break;
                default:
                    throw new InvalidOperationException($"Binary broadcast protocol handles messages of type {message.GetType()}");
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
                    Era = _coinId.Era
                },
                Coin = new CommonCoinMessage
                {
                    Agreement = _coinId.Agreement,
                    Epoch = _coinId.Epoch,
                    SignatureShare = ByteString.CopyFrom(shareBytes, 0, shareBytes.Length)
                }
            };
            return message;
        }
    }
}