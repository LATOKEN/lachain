using Phorkus.Core.Proto;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Blockchain
{
    public class HashedTransaction
    {
        public Transaction Transaction { get; }

        public UInt256 Hash {
            get
            {
                if (_hash != null)
                    return _hash;
                _hash = Transaction?.ToHash256();
                return _hash;
            }
        }

        public HashedTransaction(Transaction transaction)
        {
            Transaction = transaction;
        }

        private UInt256 _hash;
    }
}