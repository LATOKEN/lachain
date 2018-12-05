using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.RPC;

namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinTransactionFactory : ITransactionFactory
    {
        private BitcoinTransactionService _bitcoinTransactionService;

        internal BitcoinTransactionFactory()
        {
            _bitcoinTransactionService = new BitcoinTransactionService();
        }
    
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

        public IDataToSign CreateDataToSign(byte[] @from, byte[] to, byte[] value)
        {
            var longValue = Utils.ConvertHexToLong(Utils.ConvertByteArrayToString(value));
            var stringFrom = Utils.ConvertByteArrayToString(from);
            var stringTo = Utils.ConvertByteArrayToString(to);
            var stringPublicKey = Utils.ConvertByteArrayToString(_bitcoinTransactionService.PublicKey);
            var outputs = _bitcoinTransactionService.GetOutputs(stringFrom, longValue);
            var prevAmount = outputs.Value;
            var inputSz = outputs.Key.Count;
            var outputSz = 2;
            var fromBtc = new BitcoinScriptAddress(stringFrom, NBitcoin.Network.Main);
            var toBtc = new BitcoinScriptAddress(stringTo, NBitcoin.Network.Main);
            var bitcoinTransactionData = new BitcoinTransactionData();
            bitcoinTransactionData.RawTransaction = new byte[0];
            var scriptPubKeyValue = fromBtc.ScriptPubKey.ToString();
            var scriptPubKeyChange = toBtc.ScriptPubKey.ToString();
            var change = prevAmount - _bitcoinTransactionService.GetFee(inputSz, outputSz);
            var scriptCode = "1976a914"
                             + Hashes.Hash160(Utils.ConvertHexStringToByteArray(stringPublicKey)).ToString() + "88ac";
            var prevOuts = "";
            foreach (var output in outputs.Key)
            {
                prevOuts += Utils.ReverseHex(output.Hash.ToString())
                            + Utils.ConvertUIntToReversedHex(output.N, Utils.IntHexLength);
            }

            var hashedPrevOuts = Hashes.Hash256(Hashes.Hash256(
                Utils.ConvertHexStringToByteArray(prevOuts)).ToBytes()).ToString();
            var hashedOuts = Hashes.Hash256(Hashes.Hash256(Utils.ConvertHexStringToByteArray(
                Utils.ConvertLongToReversedHex(longValue)
                + scriptPubKeyValue +
                Utils.ConvertLongToReversedHex(change) +
                scriptPubKeyChange)).ToBytes()).ToString();
            var hashedSeq = Hashes.Hash256(Hashes.Hash256(
                Utils.ConvertHexStringToByteArray(BitcoinConfig.Sequence)).ToBytes()).ToString();
            var bitcoinDataToSign = new BitcoinDataToSign();
            bitcoinDataToSign.EllipticCurveType = EllipticCurveType.Secp256K1;
            bitcoinDataToSign.DataToSign = new[]
            {
                Hashes.Hash256(Hashes.Hash256(Utils.ConvertHexStringToByteArray(
                        BitcoinConfig.Version + hashedPrevOuts + hashedSeq
                        + prevOuts + scriptCode
                        + Utils.ConvertLongToReversedHex(
                            prevAmount) + BitcoinConfig.Sequence + hashedOuts
                        + BitcoinConfig.LockTime + BitcoinConfig.HashType)).ToBytes())
                    .ToBytes()
            };

            return bitcoinDataToSign;
        }

        public ITransactionData CreateRawTransaction(byte[] @from, byte[] to, byte[] value,
            IReadOnlyCollection<byte[]> signatures)
        {
            var longValue = Utils.ConvertHexToLong(Utils.ConvertByteArrayToString(value));
            var stringFrom = Utils.ConvertByteArrayToString(from);
            var stringTo = Utils.ConvertByteArrayToString(to);
            var stringPublicKey = Utils.ConvertByteArrayToString(_bitcoinTransactionService.PublicKey);
            var outputs = _bitcoinTransactionService.GetOutputs(stringFrom, longValue);
            var prevAmount = outputs.Value;
            var inputSz = outputs.Key.Count;
            var outputSz = 2;
            // String publicKey = "02e5974f3e1e9599ff5af036b5d6057d80855e7182afb4c2fa1fe38bc6efb9072b";
            var fromBtc = new BitcoinScriptAddress(stringFrom, NBitcoin.Network.Main);
            var toBtc = new BitcoinScriptAddress(stringTo, NBitcoin.Network.Main);
            var scriptPubKeyValue = fromBtc.ScriptPubKey.ToString();
            var scriptPubKeyChange = toBtc.ScriptPubKey.ToString();
            var change = prevAmount - _bitcoinTransactionService.GetFee(inputSz, outputSz);
            var prevOuts = "";
            var redeemScript =
                "17160014" + Hashes.Hash160(Utils.ConvertHexStringToByteArray(stringPublicKey)).ToString();
            foreach (var output in outputs.Key)
            {
                prevOuts += Utils.ReverseHex(output.Hash.ToString())
                            + redeemScript
                            + Utils.ConvertUIntToReversedHex(output.N,
                                Utils.IntHexLength) + BitcoinConfig.Sequence;
            }

            var signature = Utils.ConvertByteArrayToString(signatures.FirstOrDefault());
            var r = AppendSigPrefix(signature.Substring(0, 64));
            var s = AppendSigPrefix(signature.Substring(64, 64));
            var bitcoinTransactionData = new BitcoinTransactionData();
            bitcoinTransactionData.RawTransaction = Utils.ConvertHexStringToByteArray(
                BitcoinConfig.Version + BitcoinConfig.Marker + BitcoinConfig.Flag
                + Utils.ConvertIntToReversedHex(
                    inputSz, 1) + prevOuts
                + Utils.ConvertIntToReversedHex(
                    outputSz, 1)
                + Utils.ConvertLongToReversedHex(
                    longValue)
                + Utils.ConvertIntToReversedHex(
                    scriptPubKeyValue.Length / 2,
                    1)
                + scriptPubKeyValue
                + Utils.ConvertLongToReversedHex(
                    change)
                + Utils.ConvertIntToReversedHex(
                    scriptPubKeyChange.Length / 2,
                    1)
                + scriptPubKeyChange + BitcoinConfig.WitnessCode
                + Utils.ConvertIntToReversedHex(
                    r.Length / 2 + s.Length / 2 +
                    stringPublicKey.Length / 2 + 1, 1)
                + r + s + BitcoinConfig.HashType + stringPublicKey +
                BitcoinConfig.LockTime);
            return bitcoinTransactionData;
        }
    }
}