using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Numerics;
using NBitcoin.RPC;
using Nethereum.JsonRpc.Client;
using Nethereum.RLP;
using Nethereum.RPC;
using Nethereum.Signer;
using Phorkus.Proto;

namespace Phorkus.CrossChain.Ethereum
{
    public class EthereumTransactionFactory : ITransactionFactory
    {
        private const string InitV = "17";
        private const uint GasTransfer = 21000;
        private const string NullData = "00";

        public BigInteger GetNonce(string address)
        {
            var uri = new System.Uri(@"http://localhost:8545/");
            var ethApiService = new EthApiService(new RpcClient(uri, null, null));
            var getNonce = ethApiService.Transactions.GetTransactionCount.SendRequestAsync(address);
            getNonce.Wait();
            if (getNonce.IsFaulted)
            {
                throw new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED,
                    "Bad request", RPCResponse.Load(Stream.Null));
            }

            return getNonce.Result.Value;
        }

        public BigInteger GetGasPrice()
        {
            var uri = new System.Uri(@"http://localhost:8545/");
            var ethApiService = new EthApiService(new RpcClient(uri, null, null));
            var getGasPrice = ethApiService.GasPrice.SendRequestAsync();
            getGasPrice.Wait();
            if (getGasPrice.IsFaulted)
            {
                throw new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED,
                    "Bad request", RPCResponse.Load(Stream.Null));
            }

            return getGasPrice.Result.Value;
        }

        public BigInteger GetBalance(string address)
        {
            var uri = new System.Uri(@"http://localhost:8545/");
            var ethApiService = new EthApiService(new RpcClient(uri, null, null));
            var getBalance = ethApiService.GetBalance.SendRequestAsync(address);
            getBalance.Wait();
            if (getBalance.IsFaulted)
            {
                throw new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED,
                    "Bad request", RPCResponse.Load(Stream.Null));
            }

            return getBalance.Result.Value;
        }

        public IDataToSign CreateDataToSign(string publicKey, string @from, string to, long value)
        {
            var nonce = GetNonce(from);
            var gasPrice = GetGasPrice();
            var balance = GetBalance(from);
            if (balance < gasPrice * GasTransfer + value)
            {
                throw new InvalidDataException("Insufficient balance");
            }

            var ethereumDataToSign = new EthereumDataToSign();
            ethereumDataToSign.EllipticCurveType = EllipticCurveType.Secp256K1;
            ethereumDataToSign.DataToSign = new[]
            {
                RLP.EncodeElement(Utils.ConvertHexStringToByteArray(nonce.ToString("x2")
                                                                    + gasPrice.ToString("x2") +
                                                                    GasTransfer.ToString("x2")
                                                                    + Utils.AppendZero(to)
                                                                    + value.ToString("x2") + NullData + InitV +
                                                                    NullData + NullData))
            };
            return ethereumDataToSign;
        }

        public ITransactionData CreateRawTransaction(string publicKey, string @from, string to, long value,
            IReadOnlyCollection<byte[]> signatures)
        {
            var nonce = GetNonce(from);
            var gasPrice = GetGasPrice();
            var balance = GetBalance(from);
            if (balance < gasPrice * GasTransfer + value)
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
                    "x2") + GasTransfer
                    .ToString("x2")
                + to +
                value.ToString("x2") +
                NullData + v + r + s));
            return ethereumTransactionData;
        }
    }
}