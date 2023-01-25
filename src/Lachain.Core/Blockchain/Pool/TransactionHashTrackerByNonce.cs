using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Lachain.Logger;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Pool
{
    public class TransactionHashTrackerByNonce : ITransactionHashTrackerByNonce
    {
        private static readonly ILogger<TransactionHashTrackerByNonce> Logger 
            = LoggerFactory.GetLoggerForClass<TransactionHashTrackerByNonce>();
        private readonly ConcurrentDictionary<AddressWithNonce, UInt256> _hashPerAddressWithNonce
            = new ConcurrentDictionary<AddressWithNonce, UInt256>();

        public TransactionHashTrackerByNonce() { }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryAdd(TransactionReceipt receipt)
        {
            var hash = receipt.Hash;
            var nonce = receipt.Transaction.Nonce;
            var from = receipt.Transaction.From;
            var key = new AddressWithNonce(from, nonce);
            if (_hashPerAddressWithNonce.ContainsKey(key)) return false;
            return _hashPerAddressWithNonce.TryAdd(key, hash);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryRemove(TransactionReceipt receipt)
        {
            var nonce = receipt.Transaction.Nonce;
            var from = receipt.Transaction.From;
            var key = new AddressWithNonce(from, nonce);
            return _hashPerAddressWithNonce.TryRemove(key, out var _);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetTransactionHash(UInt160 address, ulong nonce, out UInt256? hash)
        {
            hash = null;
            var key = new AddressWithNonce(address, nonce);
            if (_hashPerAddressWithNonce.TryGetValue(key, out var txHash))
            {
                hash = txHash;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _hashPerAddressWithNonce.Clear();
        }
    }

    
    // This is just a helper class to create key combining address and nonce
    // to track transaction by address and nonce
    public class AddressWithNonce : IEquatable<AddressWithNonce>
    {
        private readonly UInt160 _address;
        private readonly ulong _nonce;
        public AddressWithNonce(UInt160 address, ulong nonce)
        {
            _address = address;
            _nonce = nonce;
        }
        
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((AddressWithNonce) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_address, _nonce);
        }

        public bool Equals(AddressWithNonce? other)
        {
            return !(other is null) && _nonce == other._nonce && _address.Equals(other._address);
        }
    }
}
