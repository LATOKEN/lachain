﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Lachain.Proto;
using Lachain.Utility.Utils;
using Nethereum.Signer;
using Transaction = Lachain.Proto.Transaction;

namespace Lachain.Crypto
{
    public static class TransactionUtils
    {
        private static int _oldChainId;
        private static int _newChainId;
        public static void SetChainId(int oldChainId,  int newChainId)
        {
            if (_oldChainId == 0) _oldChainId = oldChainId;
            else throw new Exception("trying to set chainId second time.");
            if (_newChainId == 0) _newChainId = newChainId;
            else throw new Exception("trying to set chainId second time.");
        }

        public static int ChainId(bool useNewId)
        {
            return useNewId ? _newChainId : _oldChainId;
        }
        
        public static TransactionChainId GetEthTx(this Transaction t, Signature? s, bool useNewId)
        {
            var nonce = t.Nonce == 0
                ? Array.Empty<byte>()
                : new BigInteger(t.Nonce).ToByteArray().Reverse().ToArray().TrimLeadingZeros();
            var sig = s is null  ?  Array.Empty<byte>() : s.Encode().AsSpan();
            var ethTx = new Nethereum.Signer.TransactionChainId(
                nonce,
                new BigInteger(t.GasPrice).ToByteArray().Reverse().ToArray().TrimLeadingZeros(),
                new BigInteger(t.GasLimit).ToByteArray().Reverse().ToArray().TrimLeadingZeros(),
                t.To.ToBytes(), // this may be empty, same as passing null
                t.Value.ToBytes(false,  true),
                t.Invocation.ToArray(),
                new BigInteger(ChainId(useNewId)).ToByteArray().Reverse().ToArray().TrimLeadingZeros(),
                sig.IsEmpty ? Array.Empty<byte>() : sig.Slice(0, 32).ToArray().TrimLeadingZeros(),
                sig.IsEmpty ? Array.Empty<byte>() : sig.Slice(32, 32).ToArray().TrimLeadingZeros(),
                sig.IsEmpty ? Array.Empty<byte>() : sig.Slice(64, 1).ToArray().TrimLeadingZeros()
            );
            return ethTx;
        }
        public static IEnumerable<byte> Rlp(this Transaction t, bool useNewId)
        {
            var ethTx = t.GetEthTx(null, useNewId);
            return ethTx.GetRLPEncodedRaw();
        }

        public static IEnumerable<byte> RlpWithSignature(this Transaction t, Signature s, bool useNewId)
        {
            var ethTx = t.GetEthTx(s, useNewId);
            return ethTx.GetRLPEncoded();
        }

        public static UInt256 RawHash(this Transaction t, bool useNewId)
        {
            return t.Rlp(useNewId).Keccak();
        }

        public static UInt256 FullHash(this Transaction t, Signature s, bool useNewId)
        {
            return t.RlpWithSignature(s, useNewId).Keccak();
        }

        public static UInt256 FullHash(this TransactionReceipt r, bool useNewId)
        {
            return r.Transaction.FullHash(r.Signature, useNewId);
        }
    }
}