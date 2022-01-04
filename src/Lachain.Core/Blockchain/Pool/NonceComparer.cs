using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Pool
{
    class NonceComparer : IComparer<KeyValuePair<ulong, UInt256>>
    {        
        public int Compare(KeyValuePair<ulong, UInt256> x, KeyValuePair<ulong, UInt256> y)
        {
            return x.Key.CompareTo(y.Key);
        }
    }
}