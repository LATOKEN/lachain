using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinTransactionFactory : ITransactionFactory
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