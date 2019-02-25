using System;
using System.Linq;
using Phorkus.Consensus.CommonCoin.ThresholdCrypto;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;
using Signature = Phorkus.Consensus.CommonCoin.ThresholdCrypto.Signature;

namespace Phorkus.Consensus.CommonCoin
{
    public class CommonCoin : ICommonCoin
    {
        private readonly IThresholdSigner _thresholdSigner;
        private readonly CoinId _coinId;
        private readonly IConsensusBroadcaster _broadcaster;

        public CommonCoin(IMessageDispatcher messageDispatcher, CoinId coinId, IConsensusBroadcaster broadcaster)
        {
            var dispatcher = messageDispatcher ?? throw new ArgumentNullException(nameof(messageDispatcher));
            _coinId = coinId ?? throw new ArgumentNullException(nameof(coinId));
            _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
            _thresholdSigner = new ThresholdSigner(
                _coinId.ToByteArray(), 
                new PrivateKeyShare(Fr.FromInt(0)), // TODO: pass keys
                new PublicKeySet(Enumerable.Empty<PublicKeyShare>(), 0)
            );

            dispatcher.RegistgerAlgorithm(this, coinId);
            _thresholdSigner.SignatureProduced += ThresholdSignerOnSignatureProduced;
        }

//        private static readonly byte[] NibbleLookup = {0, 1, 1, 2, 1, 2, 2, 3, 1, 2, 2, 3, 2, 3, 3, 4};
//
//        private static int BytePopcount(byte b)
//        {
//            return NibbleLookup[b & 0xF] + NibbleLookup[b >> 4];
//        }

        private void ThresholdSignerOnSignatureProduced(object sender, Signature signature)
        {
            CoinTossed?.Invoke(this, signature.Parity());
        }

        public void RequestCoin()
        {
            _thresholdSigner.Sign();
        }

        public void HandleMessage(ConsensusMessage message)
        {
            throw new NotImplementedException();
        }

        public event EventHandler Terminated;

        public event EventHandler<bool> CoinTossed;
    }
}