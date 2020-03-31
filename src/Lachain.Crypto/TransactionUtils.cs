using System;
using System.Linq;
using System.Numerics;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Crypto
{
    public static class TransactionUtils
    {
        public const int ChainId = 41;

        public static byte[] Rlp(this Transaction t)
        {
            var nonce = t.Nonce == 0 ? Array.Empty<byte>() : new BigInteger(t.Nonce).ToByteArray().Reverse().ToArray();
            var ethTx = new Nethereum.Signer.TransactionChainId(
                nonce,
                new BigInteger(t.GasPrice).ToByteArray().Reverse().ToArray(),
                new BigInteger(t.GasLimit).ToByteArray().Reverse().ToArray(),
                t.To.ToBytes(),
                t.Value.ToBytes(true).Reverse().ToArray(),
                Array.Empty<byte>(),
                new BigInteger(ChainId).ToByteArray().Reverse().ToArray(),
                Array.Empty<byte>(),
                Array.Empty<byte>(),
                Array.Empty<byte>()
            );
            return ethTx.GetRLPEncodedRaw();
        }
    }
}