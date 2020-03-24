using System;
using System.Linq;
using System.Numerics;
using Lachain.Proto;

namespace Lachain.Crypto
{
    public static class TransactionUtils
    {
        public const int ChainId = 41;

        public static byte[] GetRlp(Transaction t)
        {
            var nonce = t.Nonce == 0 ? Array.Empty<byte>() : new BigInteger(t.Nonce).ToByteArray().Reverse().ToArray();
            var ethTx = new Nethereum.Signer.TransactionChainId(
                nonce,
                new BigInteger(t.GasPrice).ToByteArray().Reverse().ToArray(),
                new BigInteger(t.GasLimit).ToByteArray().Reverse().ToArray(),
                t.To.Buffer.ToByteArray(),
                t.Value.Buffer.ToByteArray().Reverse().ToArray(),
                Array.Empty<byte>(),
                new BigInteger(ChainId).ToByteArray().Reverse().ToArray(),
                Array.Empty<byte>(),
                Array.Empty<byte>(),
                Array.Empty<byte>()
            );
            return ethTx.GetRLPEncodedRaw();
        }

        public static byte[] Rlp(this Transaction t)
        {
            return GetRlp(t);
        }
    }
}