﻿using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Proto;

namespace Lachain.Utility.Utils
{
    public class ReceiptComparer : IComparer<TransactionReceipt>
    {
        public int Compare(TransactionReceipt x, TransactionReceipt y)
        {
            // sort by sender, then by nonce
            if (x is null) return y is null ? 0 : -1; // nulls first, just in case
            if (y is null) return 1;
            if (!x.Transaction.From.Equals(y.Transaction.From))
            {
                return y.Transaction.From.Buffer.Cast<IComparable<byte>>()
                    .CompareLexicographically(x.Transaction.From.Buffer);
            }

            return x.Transaction.Nonce.CompareTo(y.Transaction.Nonce);
        }
    }

    public class GasPriceReceiptComparer : IComparer<TransactionReceipt>
    {
        // sort by gas price from smaller to larger
        public int Compare(TransactionReceipt x, TransactionReceipt y)
        {
            if (x is null) return y is null ? 0 : -1; // nulls first, just in case
            return y is null ? 1 : x.Transaction.GasPrice.CompareTo(y.Transaction.GasPrice);
        }
    }
}