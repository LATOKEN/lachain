using NeoSharp.Core.Cryptography;

namespace NeoSharp.Core.Models
{
    public class SignedTransaction
    {
        public Transaction Transaction { get; }
        public Signature Signature { get; }

        public SignedTransaction(Transaction transaction, Signature signature)
        {
            Transaction = transaction;
            Signature = signature;
        }
    }
}