using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using Phorkus.Proto;
using Phorkus.CrossChain;

namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinTransactionFactory : ITransactionFactory
    {
        private const string Marker = "00";
        private const string Flag = "01";
        private const string Version = "02000000";
        private const string HashType = "01000000";
        private const string Sequence = "feffffff";
        private const string WitnessCode = "02";
        private const string LockTime = "00000000";
        private const uint TxDataBytes = 120;
        private const uint InputBytes = 64;
        private const uint OutputBytes = 32;

        private string AppendSigPrefix(string signature)
        {
            var appendSig = "";
            if ((signature[0] >= '8' && signature[0] <= '9') || (signature[0] >= 'a' && signature[0] <= 'f'))
            {
                appendSig = "00" + signature;
            }
            else
            {
                appendSig = signature;
            }

            return "02" + Utils.ConvertIntToReversedHex(appendSig.Length / 2, 1) + appendSig;
        }

        public static long calcBytes(int inputsNum, int outputsNum)
        {
            return inputsNum * InputBytes + outputsNum * OutputBytes + TxDataBytes;
        }

        public static long GetFee(int inputsNum, int outputsNum)
        {
            var rpcClient = new RPCClient(NBitcoin.Network.Main);
            var estimateFee = rpcClient.EstimateSmartFeeAsync(1);
            estimateFee.Wait();
            if (estimateFee.IsFaulted)
            {
                throw new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED,
                    "Bad request", RPCResponse.Load(Stream.Null));
            }
            var satoshiPerByte = (long) (estimateFee.Result.FeeRate.SatoshiPerByte * (decimal) 1e8);
            return satoshiPerByte * calcBytes(inputsNum, outputsNum);
        }


        public IDataToSign CreateDataToSign(string publicKey, string @from, string to, long value)
        {
            var outputs = BitcoinTransactionService.GetOutputs(from, value);
            var prevAmount = outputs.Value;
            var inputSz = outputs.Key.Count;
            var outputSz = 2;
            var fromBtc = new BitcoinScriptAddress(from, NBitcoin.Network.Main);
            var toBtc = new BitcoinScriptAddress(to, NBitcoin.Network.Main);
            var bitcoinTransactionData = new BitcoinTransactionData();
            bitcoinTransactionData.RawTransaction = new byte[0];
            var scriptPubKeyValue = fromBtc.ScriptPubKey.ToString();
            var scriptPubKeyChange = toBtc.ScriptPubKey.ToString();
            var change = prevAmount - GetFee(inputSz, outputSz);
            var scriptCode = "1976a914"
                             + Hashes.Hash160(Utils.ConvertHexStringToByteArray(publicKey)).ToString() + "88ac";
            var prevOuts = "";
            foreach (var output in outputs.Key)
            {
                prevOuts += Utils.ReverseHex(output.Hash.ToString())
                            + Utils.ConvertUIntToReversedHex(output.N, Utils.IntHexLength);
            }

            var hashedPrevOuts = Hashes.Hash256(Hashes.Hash256(
                Utils.ConvertHexStringToByteArray(prevOuts)).ToBytes()).ToString();
            var hashedOuts = Hashes.Hash256(Hashes.Hash256(Utils.ConvertHexStringToByteArray(
                Utils.ConvertLongToReversedHex(value)
                + scriptPubKeyValue +
                Utils.ConvertLongToReversedHex(change) +
                scriptPubKeyChange)).ToBytes()).ToString();
            var hashedSeq = Hashes.Hash256(Hashes.Hash256(
                Utils.ConvertHexStringToByteArray(Sequence)).ToBytes()).ToString();
            var bitcoinDataToSign = new BitcoinDataToSign();
            bitcoinDataToSign.EllipticCurveType = EllipticCurveType.Secp256K1;
            bitcoinDataToSign.DataToSign = new[]
            {
                Hashes.Hash256(Hashes.Hash256(Utils.ConvertHexStringToByteArray(Version + hashedPrevOuts + hashedSeq
                                                                                + prevOuts + scriptCode
                                                                                + Utils.ConvertLongToReversedHex(
                                                                                    prevAmount) + Sequence + hashedOuts
                                                                                + LockTime + HashType)).ToBytes())
                    .ToBytes()
            };

            return bitcoinDataToSign;
        }

        public ITransactionData CreateRawTransaction(string publicKey, string @from, string to, long value,
            IReadOnlyCollection<byte[]> signatures)
        {
            var outputs = BitcoinTransactionService.GetOutputs(from, value);
            var prevAmount = outputs.Value;
            var inputSz = outputs.Key.Count;
            var outputSz = 2;
            // String publicKey = "02e5974f3e1e9599ff5af036b5d6057d80855e7182afb4c2fa1fe38bc6efb9072b";
            var fromBtc = new BitcoinScriptAddress(from, NBitcoin.Network.Main);
            var toBtc = new BitcoinScriptAddress(to, NBitcoin.Network.Main);
            var scriptPubKeyValue = fromBtc.ScriptPubKey.ToString();
            var scriptPubKeyChange = toBtc.ScriptPubKey.ToString();
            var change = prevAmount - GetFee(inputSz, outputSz);
            var prevOuts = "";
            var redeemScript = "17160014" + Hashes.Hash160(Utils.ConvertHexStringToByteArray(publicKey)).ToString();
            foreach (var output in outputs.Key)
            {
                prevOuts += Utils.ReverseHex(output.Hash.ToString()) + redeemScript
                                                                     + Utils.ConvertUIntToReversedHex(output.N,
                                                                         Utils.IntHexLength) + Sequence;
            }

            var signature = Utils.ConvertByteArrayToString(signatures.FirstOrDefault());
            var r = AppendSigPrefix(signature.Substring(0, 64));
            var s = AppendSigPrefix(signature.Substring(64, 64));
            var bitcoinTransactionData = new BitcoinTransactionData();
            bitcoinTransactionData.RawTransaction = Utils.ConvertHexStringToByteArray(Version + Marker + Flag
                                                                                      + Utils.ConvertIntToReversedHex(
                                                                                          inputSz, 1) + prevOuts
                                                                                      + Utils.ConvertIntToReversedHex(
                                                                                          outputSz, 1)
                                                                                      + Utils.ConvertLongToReversedHex(
                                                                                          value)
                                                                                      + Utils.ConvertIntToReversedHex(
                                                                                          scriptPubKeyValue.Length / 2,
                                                                                          1)
                                                                                      + scriptPubKeyValue
                                                                                      + Utils.ConvertLongToReversedHex(
                                                                                          change)
                                                                                      + Utils.ConvertIntToReversedHex(
                                                                                          scriptPubKeyChange.Length / 2,
                                                                                          1)
                                                                                      + scriptPubKeyChange + WitnessCode
                                                                                      + Utils.ConvertIntToReversedHex(
                                                                                          r.Length / 2 + s.Length / 2 +
                                                                                          publicKey.Length / 2 + 1, 1)
                                                                                      + r + s + HashType + publicKey +
                                                                                      LockTime);
            return bitcoinTransactionData;
        }
    }
}