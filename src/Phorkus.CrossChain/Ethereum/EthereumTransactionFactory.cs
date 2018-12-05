using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using NBitcoin.RPC;
using Nethereum.JsonRpc.Client;
using Nethereum.RLP;
using Nethereum.RPC;

namespace Phorkus.CrossChain.Ethereum
{
    public class EthereumTransactionFactory : ITransactionFactory
    {
        private EthereumTransactionService _ethereumTransactionService;

        internal EthereumTransactionFactory()
        {
            _ethereumTransactionService = new EthereumTransactionService();
        }

        public IDataToSign CreateDataToSign(byte[] @from, byte[] to, byte[] value)
        {
            var stringFrom = Utils.ConvertByteArrayToString(from);
            var stringTo = Utils.ConvertByteArrayToString(to);
            var longValue = Utils.ConvertHexToLong(Utils.ConvertByteArrayToString(value));
            var nonce = _ethereumTransactionService.GetNonce(stringFrom);
            var gasPrice = _ethereumTransactionService.GetGasPrice();
            var balance = _ethereumTransactionService.GetBalance(stringFrom);
            if (balance < gasPrice * EthereumConfig.GasTransfer + longValue)
            {
                throw new InvalidDataException("Insufficient balance");
            }

            var ethereumDataToSign = new EthereumDataToSign();
            ethereumDataToSign.EllipticCurveType = EllipticCurveType.Secp256K1;
            ethereumDataToSign.DataToSign = new[]
            {
                RLP.EncodeElement(Utils.ConvertHexStringToByteArray(
                    nonce.ToString("x2")
                    + gasPrice.ToString("x2") +
                    EthereumConfig.GasTransfer.ToString("x2")
                    + Utils.AppendZero(stringTo)
                    + longValue.ToString("x2") + EthereumConfig.NullData + EthereumConfig.InitV +
                    EthereumConfig.NullData + EthereumConfig.NullData))
            };
            return ethereumDataToSign;
        }

        public ITransactionData CreateRawTransaction(byte[] @from, byte[] to, byte[] value,
            IReadOnlyCollection<byte[]> signatures)
        {
            var stringFrom = Utils.ConvertByteArrayToString(from);
            var stringTo = Utils.ConvertByteArrayToString(to);
            var longValue = Utils.ConvertHexToLong(Utils.ConvertByteArrayToString(value));
            var nonce = _ethereumTransactionService.GetNonce(stringFrom);
            var gasPrice = _ethereumTransactionService.GetGasPrice();
            var balance = _ethereumTransactionService.GetBalance(stringFrom);
            if (balance < gasPrice * EthereumConfig.GasTransfer + longValue)
            {
                throw new InvalidDataException("Insufficient balance");
            }

            var signature = Utils.ConvertByteArrayToString(signatures.FirstOrDefault());
            var r = signature.Substring(0, 64);
            var s = signature.Substring(64, 64);
            var v = signature.Substring(128, 2);
            var ethereumTransactionData = new EthereumTransactionData();
            ethereumTransactionData.RawTransaction = RLP.EncodeElement(Utils.ConvertHexStringToByteArray(
                nonce.ToString("x2")
                + gasPrice.ToString(
                    "x2") + EthereumConfig.GasTransfer
                    .ToString("x2")
                + Utils.AppendZero(stringTo) +
                longValue.ToString("x2") +
                EthereumConfig.NullData + v + r + s));
            return ethereumTransactionData;
        }
    }
}