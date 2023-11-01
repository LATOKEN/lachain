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
        private readonly ConcurrentDictionary<UInt160, ulong> _noncePerAddress
            = new ConcurrentDictionary<UInt160, ulong>();
        
        // tracks the transaction hashes by their sender's address and nonce
        private readonly ITransactionHashTrackerByNonce _transactionHashTracker;

        public NonceCalculator(ITransactionHashTrackerByNonce transactionHashTracker)
        {
            _transactionHashTracker = transactionHashTracker;
        }

        // try to add a transaction in the data structure
        // returns false if it already exists, otherwise adds it to the structure and returns true

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryAdd(TransactionReceipt receipt)
        {
            var nonce = receipt.Transaction.Nonce;
            var from = receipt.Transaction.From;

            if(_noncePerAddress.TryGetValue(from, out var maxNonce))
            {
                if (nonce > maxNonce)
                {
                    _noncePerAddress[from] = nonce;
                }
                _transactionHashTracker.TryAdd(receipt);
                return true;
            }
            else
            {
                _noncePerAddress[from] = nonce;
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
            var nonce = receipt.Transaction.Nonce;
            var from = receipt.Transaction.From;

            if(_noncePerAddress.TryGetValue(from, out var maxNonce))
            {
                _transactionHashTracker.TryRemove(receipt);
                if (nonce == maxNonce)
                {
                    // txes are taken or removed sequentially
                    // if max nonce is removed that means all txes from this sender is removed
                    return _noncePerAddress.TryRemove(from, out var _);
                }
                return true;
            }
            else
            {
                return false; 
            }
        }

        // given an address, returns the max nonce of all the transactions by this address
        // if there is no such transaction, returns null
        
        public ulong? GetMaxNonceForAddress(UInt160 address) 
        {
            if(!_noncePerAddress.TryGetValue(address, out var maxNonce))
                return null;
            
            return maxNonce;
        }

        public void Clear()
        {
            _noncePerAddress.Clear();
        }
   }
}