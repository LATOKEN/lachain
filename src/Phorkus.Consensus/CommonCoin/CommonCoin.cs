using System;
using System.Linq;
using Google.Protobuf;
using Phorkus.Consensus.CommonCoin.ThresholdCrypto;
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
        private readonly PrivateKeyShare _privateKeyShare;
        private bool _terminated;

        public CommonCoin(
            PublicKeySet publicKeySet, PrivateKeyShare privateKeyShare,
            CoinId coinId, IConsensusBroadcaster broadcaster
        )
        {
            _publicKeySet = publicKeySet ?? throw new ArgumentNullException(nameof(publicKeySet));
            _coinId = coinId ?? throw new ArgumentNullException(nameof(coinId));
            _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
            _privateKeyShare = privateKeyShare ?? throw new ArgumentNullException(nameof(privateKeyShare));
            _thresholdSigner = new ThresholdSigner(_coinId.ToByteArray(), privateKeyShare, publicKeySet);
            _thresholdSigner.SignatureProduced += ThresholdSignerOnSignatureProduced;
        }

        private void ThresholdSignerOnSignatureProduced(object sender, Signature signature)
        {
            _terminated = true;
            CoinTossed?.Invoke(this, signature.Parity());
        }

        public void RequestCoin()
        {
            var signatureShare = _thresholdSigner.Sign();
            _thresholdSigner.AddShare(_privateKeyShare.GetPublicKeyShare(), signatureShare);
            _broadcaster.Broadcast(CreateCoinMessage(signatureShare));
        }

        public IProtocolIdentifier Id => _coinId;

        public void HandleMessage(ConsensusMessage message)
        {
            if (_terminated) return;
            // These checks are somewhat redundant, but whatever
            if (message.PayloadCase != ConsensusMessage.PayloadOneofCase.CommonCoin)
                throw new ArgumentException(
                    $"consensus message of type {message.PayloadCase} routed to CommonCoin protocol");
            if (message.Validator.Era != _coinId.Era ||
                message.CommonCoin.Agreement != _coinId.Agreement ||
                message.CommonCoin.Epoch != _coinId.Epoch)
                throw new ArgumentException("era, agreement or epoch of message mismatched");

            _thresholdSigner.AddShare(
                _publicKeySet[(int) message.Validator.ValidatorIndex],
                SignatureShare.FromBytes(message.CommonCoin.SignatureShare.ToByteArray())
            );
        }

        public event EventHandler Terminated;

        public event EventHandler<bool> CoinTossed;

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
                CommonCoin = new CommonCoinPayload
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