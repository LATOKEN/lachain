using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Crypto
{
    public static class TransactionUtils
    {
        public const int ChainId = 41;

        public static IEnumerable<byte> Rlp(this Transaction t)
        {
            var nonce = t.Nonce == 0 ? Array.Empty<byte>() : new BigInteger(t.Nonce).ToByteArray().Reverse().ToArray().TrimLeadingZeros();
            var ethTx = new Nethereum.Signer.TransactionChainId(
                nonce,
                new BigInteger(t.GasPrice).ToByteArray().Reverse().ToArray().TrimLeadingZeros(),
                new BigInteger(t.GasLimit).ToByteArray().Reverse().ToArray().TrimLeadingZeros(),
                t.To.ToBytes(), // this may be empty, same as passing null
                t.Value.ToBytes(false, true).ToArray().TrimLeadingZeros(),
                t.Invocation.ToArray().TrimLeadingZeros(),
                new BigInteger(ChainId).ToByteArray().Reverse().ToArray().TrimLeadingZeros(),
                Array.Empty<byte>(),
                Array.Empty<byte>(),
                Array.Empty<byte>()
            );
            return ethTx.GetRLPEncodedRaw();
        }

        public static IEnumerable<byte> RlpWithSignature(this Transaction t, Signature s)
        {
            var nonce = t.Nonce == 0 ? Array.Empty<byte>() : new BigInteger(t.Nonce).ToByteArray().Reverse().ToArray().TrimLeadingZeros();
            var sig = s.Encode().AsSpan();
            var ethTx = new Nethereum.Signer.TransactionChainId(
                nonce,
                new BigInteger(t.GasPrice).ToByteArray().Reverse().ToArray().TrimLeadingZeros(),
                new BigInteger(t.GasLimit).ToByteArray().Reverse().ToArray().TrimLeadingZeros(),
                t.To.ToBytes(), // this may be empty, same as passing null
                t.Value.ToBytes(true).Reverse().ToArray().TrimLeadingZeros(),
                t.Invocation.ToArray().TrimLeadingZeros(),
                new BigInteger(ChainId).ToByteArray().Reverse().ToArray().TrimLeadingZeros(),
                sig.Slice(0, 32).ToArray().TrimLeadingZeros(),
                sig.Slice(32, 32).ToArray().TrimLeadingZeros(),
                sig.Slice(64, 1).ToArray().TrimLeadingZeros()
            );
            return ethTx.GetRLPEncoded();
        }
        
        public static UInt256 RawHash(this Transaction t)
        {
            return t.Rlp().Keccak();
        }
        
        public static UInt256 FullHash(this Transaction t, Signature s)
        {
            return t.RlpWithSignature(s).Keccak();
        }

        public static UInt256 FullHash(this TransactionReceipt r)
        {
            return r.Transaction.FullHash(r.Signature);
        }
    }
}