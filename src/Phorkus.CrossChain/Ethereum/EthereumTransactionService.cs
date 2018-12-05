using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NBitcoin.RPC;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;
using Phorkus.CrossChain.Bitcoin;

namespace Phorkus.CrossChain.Ethereum
{
    public class EthereumTransactionService : ITransactionService
    {
        private EthApiService _ethApiService;

        internal EthereumTransactionService()
        {
            _ethApiService = new EthApiService(new RpcClient(new System.Uri(EthereumConfig.RpcUri), null, null));
        }

        public BigInteger GetNonce(string address)
        {
            var getNonce = _ethApiService.Transactions.GetTransactionCount.SendRequestAsync(address);
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
            var getGasPrice = _ethApiService.GasPrice.SendRequestAsync();
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
            var getBalance = _ethApiService.GetBalance.SendRequestAsync(address);
            getBalance.Wait();
            if (getBalance.IsFaulted)
            {
                throw new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED,
                    "Bad request", RPCResponse.Load(Stream.Null));
            }

            return getBalance.Result.Value;
        }

        public ulong CurrentBlockHeight { get; }

        public BigInteger GetLastBlockHeight()
        {
            var getBlockHeight = _ethApiService.Blocks.GetBlockNumber.SendRequestAsync();
            getBlockHeight.Wait();

            if (getBlockHeight.IsFaulted)
            {
                throw new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED,
                    "Bad request", RPCResponse.Load(Stream.Null));
            }

            return getBlockHeight.Result.Value;
        }

        public IEnumerable<IContractTransaction> GetTransactionsAtBlock(byte[] recipient, ulong blockHeight)
        {
            var getTransactions =
                _ethApiService.Blocks.GetBlockWithTransactionsByHash.SendRequestAsync(_ethApiService.Blocks
                    .GetBlockNumber.ToString());
            getTransactions.Wait();
            if (getTransactions.IsFaulted)
            {
                throw new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED,
                    "Bad request", RPCResponse.Load(Stream.Null));
            }

            var address = Utils.ConvertByteArrayToString(recipient);
            var transactions = new List<EthereumContractTransaction>();
            foreach (var tx in getTransactions.Result.Transactions)
            {
                if (tx.To == address)
                {
                    transactions.Add(new EthereumContractTransaction(BlockchainType.Ethereum,
                        Utils.ConvertHexStringToByteArray(tx.From), AddressFormat.Ripmd160, tx.Value.Value));
                }
            }

            return transactions;
        }

        public bool BroadcastTransactionsBatch(ITransactionData[] transactionData)
        {
            return true;
        }

        public bool StoreTransaction(ITransactionData transactionData)
        {
            return true;
        }

        public bool BroadcastTransaction(ITransactionData transactionData)
        {
            var sendTransaction =
                _ethApiService.Transactions.SendRawTransaction.SendRequestAsync(
                    Utils.ConvertByteArrayToString(transactionData.RawTransaction));
            sendTransaction.Wait();
            if (sendTransaction.IsFaulted)
            {
                throw new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED,
                    "Bad request", RPCResponse.Load(Stream.Null));
            }

            return sendTransaction.IsCompleted;
        }
    }
}