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
    public class TransactionHashTrackerByNonce
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
    }

    public class AddressWithNonce
    {
        public UInt160 _address;
        public ulong _nonce;
        public AddressWithNonce(UInt160 address, ulong nonce)
        {
            _address = address;
            _nonce = nonce;
        }
    }
}
