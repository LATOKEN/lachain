using System.Collections.Generic;
using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.Core.Blockchain.Interface
{
    public interface ITransactionBuilder
    {
        Transaction TransferTransaction(UInt160 from, UInt160 to, Money value, byte[]? input = null);
        
        Transaction DeployTransaction(UInt160 from, IEnumerable<byte> byteCode, byte[]? input = null);

        Transaction TokenTransferTransaction(UInt160 contract, UInt160 from, UInt160 to, Money value);
    }
}