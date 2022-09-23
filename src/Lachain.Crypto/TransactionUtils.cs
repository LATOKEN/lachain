using System;
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
        
        public static RLPSigner GetEthTx(this Transaction t, Signature? s, bool useNewId)
        {
            var nonce = t.Nonce == 0
                ? Array.Empty<byte>()
                : new BigInteger(t.Nonce).ToByteArray().Reverse().ToArray().TrimLeadingZeros();
            var sig = s is null  ?  Array.Empty<byte>() : s.Encode().AsSpan();
            var rlpSigner = new Nethereum.Signer.RLPSigner(new byte[][]{
                nonce,
                new BigInteger(t.GasPrice).ToByteArray().Reverse().ToArray().TrimLeadingZeros(),
                new BigInteger(t.GasLimit).ToByteArray().Reverse().ToArray().TrimLeadingZeros(),
                t.To.ToBytes(), // this may be empty, same as passing null
                t.Value.ToBytes(false,  true),
                t.Invocation.ToArray(),
                new BigInteger(ChainId(useNewId)).ToByteArray().Reverse().ToArray().TrimLeadingZeros(), null, null},
                sig.IsEmpty ? Array.Empty<byte>() : sig.Slice(0, 32).ToArray().TrimLeadingZeros(),
                sig.IsEmpty ? Array.Empty<byte>() : sig.Slice(32, 32).ToArray().TrimLeadingZeros(),
                sig.IsEmpty ? Array.Empty<byte>() : sig.Slice(64, sig.Length - 64).ToArray().TrimLeadingZeros(), 
                6
            );
            return rlpSigner;
        }
        public static IEnumerable<byte> Rlp(this Transaction t, bool useNewId)
        {
            var ethTx = t.GetEthTx(null, useNewId);
            return ethTx.GetRLPEncodedRaw();
        }
        
        public static byte[] LAEncodeSigned(SignedData signedData, int numberOfElements)
        {
            List<byte[]> numArrayList = new List<byte[]>();
            for (int index = 0; index < numberOfElements; ++index)
                numArrayList.Add(Nethereum.RLP.RLP.EncodeElement(signedData.Data[index]));
            byte[] numArray1;
            byte[] numArray2;
            byte[] numArray3;
            if (signedData.IsSigned())
            {
                numArray1 = Nethereum.RLP.RLP.EncodeElement(signedData.V);
                numArray2 = Nethereum.RLP.RLP.EncodeElement(signedData.R);
                numArray3 = Nethereum.RLP.RLP.EncodeElement(signedData.S);
            }
            else
            {
                numArray1 = Nethereum.RLP.RLP.EncodeElement(Nethereum.Model.DefaultValues.EMPTY_BYTE_ARRAY);
                numArray2 = Nethereum.RLP.RLP.EncodeElement(Nethereum.Model.DefaultValues.EMPTY_BYTE_ARRAY);
                numArray3 = Nethereum.RLP.RLP.EncodeElement(Nethereum.Model.DefaultValues.EMPTY_BYTE_ARRAY);
            }
            numArrayList.Add(numArray1);
            numArrayList.Add(numArray2);
            numArrayList.Add(numArray3);
            return Nethereum.RLP.RLP.EncodeList(numArrayList.ToArray());
        }

        public static IEnumerable<byte> RlpWithSignature(this Transaction t, Signature s, bool useNewId)
        {
            var ethTx = t.GetEthTx(s, useNewId);
            // TODO: This method differs from the RLPSigmer.GetRLPEncoded from Nethereum version >= 4.0.0
            // for transactions with zero signature (we use such txes in genesis block and for system txes)
            // So our serialization of such txes differs from ethereum serialization,  need to fix it in future
            // (requires hardfork)
            return LAEncodeSigned(new SignedData( ethTx.Data, ethTx.Signature), 6);
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
