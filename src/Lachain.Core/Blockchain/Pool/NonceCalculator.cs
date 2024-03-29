using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Proto;


namespace Lachain.Core.Blockchain.Pool
{
    /*
        NonceCalculator Object is a data structure which supports following operations. 
        (1) add/remove a new tranaction
        (2) calculate max nonce over all transactions for a given address

        To implement the data structure, we use a dictionary keyed by address. The value of the dictionary
        is a sorted Set of (nonce, hash) for the key address.
    */

    public class NonceCalculator : INonceCalculator
    {
        private static readonly ILogger<NonceCalculator> Logger = LoggerFactory.GetLoggerForClass<NonceCalculator>();
        private readonly ConcurrentDictionary<UInt160, SortedSet<KeyValuePair<ulong, UInt256>>> _noncePerAddress
            = new ConcurrentDictionary<UInt160, SortedSet<KeyValuePair<ulong, UInt256>>>();
        
        // tracks the transaction hashes by their sender's address and nonce
        private readonly ITransactionHashTrackerByNonce _transactionHashTracker;
        
        // the count of transactions in the stucture
        private uint _count = 0;

        public NonceCalculator(ITransactionHashTrackerByNonce transactionHashTracker)
        {
            _transactionHashTracker = transactionHashTracker;
        }

        // try to add a transaction in the data structure
        // returns false if it already exists, otherwise adds it to the structure and returns true

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryAdd(TransactionReceipt receipt)
        {
            var hash = receipt.Hash;
            var nonce = receipt.Transaction.Nonce;
            var from = receipt.Transaction.From;

            var kv = new KeyValuePair<ulong, UInt256>(nonce, hash);

            if(_noncePerAddress.TryGetValue(from, out var nonces))
            {
                bool isAdded = nonces.Add(kv);
                if (isAdded)
                {
                    _count++;
                    _transactionHashTracker.TryAdd(receipt);
                }
                return isAdded;
            }
            else
            {
                var emptyNonces = new SortedSet<KeyValuePair<ulong, UInt256>>(new NonceComparer());
                emptyNonces.Add(kv);
                _noncePerAddress.TryAdd(from, emptyNonces);
                _count++;
                _transactionHashTracker.TryAdd(receipt);
                return true; 
            }
        }

        // try to remove a transaction from the data structure
        // returns false if it does not exist, 
        // otherwise removes it and returns true

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

                if (canRemove)
                {
                    _count--;
                    _transactionHashTracker.TryRemove(receipt);
                }
                return canRemove;
            }
            else
            {
                return false; 
            }
        }

        // given an address, returns the max nonce of all the transactions by this address
        // if there is no such transaction, returns null
        
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

        public uint Count()
        {
            return _count;
        }
   }
}