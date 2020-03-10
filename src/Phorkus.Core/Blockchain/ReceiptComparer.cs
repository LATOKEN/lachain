using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain
{
    public class ReceiptComparer : IComparer<TransactionReceipt>
    {
        public int Compare(TransactionReceipt x, TransactionReceipt y)
        {
            if (x is null) return y is null ? 0 : -1; // nulls first, just in case
            if (y is null) return 1;
            if (!x.Transaction.From.Equals(y.Transaction.From))
            {
                return x.Transaction.From.Buffer.Cast<IComparable<byte>>()
                    .CompareLexicographically(y.Transaction.From.Buffer);
            }

            return x.Transaction.Nonce.CompareTo(y.Transaction.Nonce);
        }
    }
}