using System.Collections.Generic;

namespace Phorkus.CrossChain
{
    public interface ITransactionFactory
    {
        IDataToSign CreateDataToSign(byte[] from, byte[] to, byte[] value);

        ITransactionData CreateRawTransaction(byte[] from, byte[] to, byte[] value,
            IEnumerable<byte[]> signatures);
    }
}