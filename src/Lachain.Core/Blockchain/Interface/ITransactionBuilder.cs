using System.Collections.Generic;
using System.Numerics;
using Lachain.Proto;
using Lachain.Utility;

namespace Lachain.Core.Blockchain.Interface
{
    public interface ITransactionBuilder
    {
        Transaction TransferTransaction(UInt160 from, UInt160 to, Money value, ulong gasPrice = 0, byte[]? input = null);
        Transaction DeployTransaction(UInt160 from, IEnumerable<byte> byteCode, byte[]? input = null);
        Transaction TokenTransferTransaction(UInt160 contract, UInt160 from, UInt160 to, Money value);

        Transaction InvokeTransaction(
            UInt160 from, UInt160 contract, Money value, string methodSignature, params dynamic[] values
        );
        Transaction InvokeTransactionWithGasPrice(
            UInt160 from, UInt160 contract, Money value, string methodSignature, ulong gasPrice, params dynamic[] values
        );

    }
}