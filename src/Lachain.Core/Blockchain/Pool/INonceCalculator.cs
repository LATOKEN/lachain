using System;
using System.Collections.Generic;
using Lachain.Core.Blockchain.Error;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Pool
{
    public interface INonceCalculator
    {
        public bool TryAdd(TransactionReceipt receipt);
        public bool TryRemove(TransactionReceipt receipt);
        ulong? GetMaxNonceForAddress(UInt160 address);
        void Clear();
    }
}