using System;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.VM
{
    public class InvocationContext
    {
        public UInt160 Sender { get; }

        public UInt256 Value => _transaction?.Transaction.Value ?? UInt256Utils.Zero;

        public UInt256 TransactionHash => _transaction?.FullHash() ?? throw new InvalidOperationException();
        
        private readonly TransactionReceipt? _transaction;

        public InvocationContext(UInt160 sender, TransactionReceipt? transaction = null)
        {
            Sender = sender;
            _transaction = transaction;
        }
        
        public InvocationContext NextContext(UInt160 caller)
        {
            return new InvocationContext(caller, _transaction);
        }
    }
}