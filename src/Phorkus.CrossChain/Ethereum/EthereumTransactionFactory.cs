using System.Collections.Generic;
using System.Linq;
using Nethereum.RLP;
using Org.BouncyCastle.Crypto.Digests;

namespace Phorkus.CrossChain.Ethereum
{
    public class EthereumTransactionFactory : ITransactionFactory
    {
        private readonly EthereumTransactionService _ethereumTransactionService;

        internal EthereumTransactionFactory()
        {
            _ethereumTransactionService = new EthereumTransactionService();
        }

        public IReadOnlyCollection<DataToSign> CreateDataToSign(byte[] from, byte[] to, byte[] value)
        {
            var stringFrom = Utils.ConvertByteArrayToString(from);
            var stringTo = Utils.ConvertByteArrayToString(to);
            var longValue = Utils.ConvertHexToLong(Utils.ConvertByteArrayToString(value));
            var nonce = _ethereumTransactionService.GetNonce(stringFrom);
            var gasPrice = _ethereumTransactionService.GetGasPrice();
            var balance = _ethereumTransactionService.GetBalance(stringFrom);
            if (balance < gasPrice * EthereumConfig.GasTransfer + longValue)
                throw new InsufficientFundsException("Insufficient balance");
            var rawTx = RLP.EncodeElement(Utils.ConvertHexStringToByteArray(
                nonce.ToString("x2")
                + gasPrice.ToString("x2") +
                EthereumConfig.GasTransfer.ToString("x2")
                + Utils.AppendZero(stringTo)
                + longValue.ToString("x2") + EthereumConfig.NullData + EthereumConfig.InitV +
                EthereumConfig.NullData + EthereumConfig.NullData));
            var digest = new Sha3Digest();
            digest.BlockUpdate(rawTx, 0, rawTx.Length);
            var sha3 = new byte[digest.GetDigestSize()];
            digest.DoFinal(sha3, 0);
            var ethereumDataToSign = new DataToSign
            {
                EllipticCurveType = EllipticCurveType.Secp256K1,
                TransactionHash = sha3
            };
            return new[]
            {
                ethereumDataToSign
            };
        }

        public RawTransaction CreateRawTransaction(byte[] @from, byte[] to, byte[] value,
            IEnumerable<byte[]> signatures)
        {
            var stringFrom = Utils.ConvertByteArrayToString(from);
            var stringTo = Utils.ConvertByteArrayToString(to);
            var longValue = Utils.ConvertHexToLong(Utils.ConvertByteArrayToString(value));
            var nonce = _ethereumTransactionService.GetNonce(stringFrom);
            var gasPrice = _ethereumTransactionService.GetGasPrice();
            var balance = _ethereumTransactionService.GetBalance(stringFrom);
            if (balance < gasPrice * EthereumConfig.GasTransfer + longValue)
                throw new InsufficientFundsException("Insufficient balance");
            var signature = Utils.ConvertByteArrayToString(signatures.FirstOrDefault());
            var r = signature.Substring(0, 64);
            var s = signature.Substring(64, 64);
            var v = signature.Substring(128, 2);
            var ethereumTransactionData = new RawTransaction
            {
                TransactionData = RLP.EncodeElement(Utils.ConvertHexStringToByteArray(
                    nonce.ToString("x2")
                    + gasPrice.ToString(
                        "x2") + EthereumConfig.GasTransfer
                        .ToString("x2")
                    + Utils.AppendZero(stringTo) +
                    longValue.ToString("x2") +
                    EthereumConfig.NullData + v + r + s))
            };
            return ethereumTransactionData;
        }
    }
}