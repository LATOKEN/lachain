using System.Collections.Generic;

namespace Phorkus.CrossChain
{
    public interface ITransactionFactory
    {
        IReadOnlyCollection<DataToSign> CreateDataToSign(byte[] from, byte[] to, byte[] value);

        RawTransaction CreateRawTransaction(byte[] from, byte[] to, byte[] value,
            IEnumerable<byte[]> signatures);
    }
}