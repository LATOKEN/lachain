using Phorkus.Core.Utils;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.VM
{
    public class InvocationContext
    {
        public UInt160 Sender { get; }

        public UInt256 Value => _transaction?.Value ?? UInt256Utils.Zero;

        public UInt256 TransactionHash => _transaction?.ToHash256() ?? UInt256Utils.Zero;

        public ulong BlockHeight => _block?.Header?.Index ?? 0;

        public UInt256 BlockHash => _block?.Hash ?? UInt256Utils.Zero;

        public ulong BlockNonce => _block?.Header?.Nonce ?? 0;

        private readonly Transaction? _transaction;
        private readonly Block? _block;
        
        public InvocationContext(UInt160 sender, Transaction? transaction = null, Block? block = null)
        {
            Sender = sender;
            _transaction = transaction;
            _block = block;
        }
        
        public InvocationContext NextContext(UInt160 caller)
        {
            return new InvocationContext(caller, _transaction, _block);
        }
    }
}