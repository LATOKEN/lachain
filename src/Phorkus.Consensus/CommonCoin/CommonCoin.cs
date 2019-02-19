using System;
using Phorkus.Proto;

namespace Phorkus.Consensus.CommonCoin
{
    public class CommonCoin : ICommonCoin
    {
        private readonly IThresholdSigner _thresholdSigner;
        private readonly CoinId _coinId;

        public CommonCoin(IMessageDispatcher messageDispatcher, CoinId coinId)
        {
            var dispatcher = messageDispatcher ?? throw new ArgumentNullException(nameof(messageDispatcher));
            _thresholdSigner = new ThresholdSigner(_coinId.ToByteArray());
            _coinId = coinId;
            
            dispatcher.RegistgerAlgorithm(this, coinId);
            _thresholdSigner.SignatureProduced += ThresholdSignerOnSignatureProduced;
        }

        private void ThresholdSignerOnSignatureProduced(object sender, byte[] e)
        {
            CoinTossed?.Invoke(this, (e[0] & 1) == 1);
        }

        public void RequestCoin()
        {
            _thresholdSigner.Sign();
        }
        
        public void HandleMessage(ConsensusMessage message)
        {
            throw new NotImplementedException();
        }

        public event EventHandler<bool> CoinTossed;
    }
}