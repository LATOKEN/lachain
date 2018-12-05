using System.Collections.Generic;
using Phorkus.Proto;


namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinTransactionData : ITransactionData
    {
        public byte[] RawTransaction { get; set; }
    }
    
}