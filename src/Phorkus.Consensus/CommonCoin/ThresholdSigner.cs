using System;

namespace Phorkus.Consensus.CommonCoin
{
    internal class ThresholdSigner : IThresholdSigner
    {
        private readonly byte[] _dataToSign;
        
        public ThresholdSigner(byte[] dataToSign)
        {
            _dataToSign = dataToSign;
        }
        
        public void Sign()
        {
            throw new NotImplementedException();
        }

        protected void OnSignatureProduced(byte[] bytes)
        {
            SignatureProduced?.Invoke(this, bytes);
        }

        public event EventHandler<byte[]> SignatureProduced;
    }
}