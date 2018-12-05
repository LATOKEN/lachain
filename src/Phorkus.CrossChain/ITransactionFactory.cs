using System;
using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.CrossChain
{
    public interface ITransactionFactory
    {
        IDataToSign CreateDataToSign(string publicKey, string from, string to, long value);
        
        ITransactionData CreateRawTransaction(string publicKey, string from, string to, long value, 
            IReadOnlyCollection<byte[]> signatures);
    }
}