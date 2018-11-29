using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.CrossChain
{
    public interface ITransactionFactory
    {
        IDataToSign CreateDataToSign(byte[] from, byte[] to, UInt256 value);
        
        ITransactionData CreateTransaction(byte[] from, byte[] to, UInt256 value, IReadOnlyCollection<byte[]> signatures);
    }
}