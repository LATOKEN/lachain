using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Pool
{
    public class NonceCalculator : INonceCalculator
    {
        private static readonly ILogger<NonceCalculator> Logger = LoggerFactory.GetLoggerForClass<NonceCalculator>();
        private readonly ConcurrentDictionary<UInt160, SortedSet<KeyValuePair<ulong, UInt256>>> _noncePerAddress
            = new ConcurrentDictionary<UInt160, SortedSet<KeyValuePair<ulong, UInt256>>>();

        public NonceCalculator() {}

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryAdd(TransactionReceipt receipt)
        {
            var hash = receipt.Hash;
            var nonce = receipt.Transaction.Nonce;
            var from = receipt.Transaction.From;

            var kv = new KeyValuePair<ulong, UInt256>(nonce, hash);

            if(_noncePerAddress.TryGetValue(from, out var nonces))
            {
                return nonces.Add(kv);
            }
            else
            {
                var emptyNonces = new SortedSet<KeyValuePair<ulong, UInt256>>(new NonceComparer());
                emptyNonces.Add(kv);
                _noncePerAddress.TryAdd(from, emptyNonces);
                return true; 
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryRemove(TransactionReceipt receipt)
        {
            var hash = receipt.Hash;
            var nonce = receipt.Transaction.Nonce;
            var from = receipt.Transaction.From;

            var kv = new KeyValuePair<ulong, UInt256>(nonce, hash);

            if(_noncePerAddress.TryGetValue(from, out var nonces))
            {
                bool canRemove = nonces.Remove(kv);
                if(nonces.Count == 0)
                {
                    _noncePerAddress.TryRemove(from, out var _);
                }
                return canRemove;
            }
            else
            {
                return false; 
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong? GetMaxNonceForAddress(UInt160 address) 
        {
            if(!_noncePerAddress.TryGetValue(address, out var nonces))
                return null;
            
            return nonces.Max.Key;
        }

        public void Clear()
        {
            _noncePerAddress.Clear();
        }
   }
}