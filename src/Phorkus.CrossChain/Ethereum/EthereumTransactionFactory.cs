using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.CrossChain.Ethereum
{
    public class EthereumTransactionFactory : ITransactionFactory
    {
        public IDataToSign CreateDataToSign(byte[] @from, byte[] to, UInt256 value)
        {
            throw new System.NotImplementedException();
        }

        public ITransactionData CreateTransaction(byte[] @from, byte[] to, UInt256 value, IReadOnlyCollection<byte[]> signatures)
        {
            throw new System.NotImplementedException();
        }
    }
}