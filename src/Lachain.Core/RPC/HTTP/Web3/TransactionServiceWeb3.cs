using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using AustinHarris.JsonRpc;
using Google.Protobuf;
using Newtonsoft.Json.Linq;
using Lachain.Core.Blockchain;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.VM;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.JSON;
using Lachain.Utility.Utils;
using Transaction = Lachain.Proto.Transaction;

namespace Lachain.Core.RPC.HTTP.Web3
{
    public class TransactionServiceWeb3 : JsonRpcService
    {
        private readonly IVirtualMachine _virtualMachine;
        private readonly IStateManager _stateManager;
        private readonly ITransactionManager _transactionManager;
        private readonly ITransactionPool _transactionPool;

        public TransactionServiceWeb3(
            IVirtualMachine virtualMachine,
            IStateManager stateManager,
            ITransactionManager transactionManager,
            ITransactionPool transactionPool)
        {
            _virtualMachine = virtualMachine;
            _stateManager = stateManager;
            _transactionManager = transactionManager;
            _transactionPool = transactionPool;
        }

        [JsonRpcMethod("eth_verifyRawTransaction")]
        private string VerifyRawTransaction(string rawTx)
        {
            var ethTx = new TransactionChainId(rawTx.HexToBytes());
            var signature = ethTx.Signature.R.Concat(ethTx.Signature.S).Concat(ethTx.Signature.V).ToSignature();
            try
            {
                var transaction = new Transaction
                {
                    // this is special case where empty uint160 is allowed
                    To = ethTx.ReceiveAddress?.ToUInt160() ?? new UInt160 {Buffer = ByteString.Empty},
                    Value = ethTx.Value.Reverse().ToArray().ToUInt256(true),
                    From = ethTx.Key.GetPublicAddress().HexToBytes().ToUInt160(),
                    Nonce = Convert.ToUInt64(ethTx.Nonce.ToHex(), 16),
                    GasPrice = Convert.ToUInt64(ethTx.GasPrice.ToHex(), 16),
                    GasLimit = Convert.ToUInt64(ethTx.GasLimit.ToHex(), 16),
                };

                var txHash = transaction.FullHash(signature);
                var result = _transactionManager.Verify(new TransactionReceipt
                {
                    Hash = txHash,
                    Signature = signature,
                    Status = TransactionStatus.Pool,
                    Transaction = transaction
                });

                if (result != OperatingError.Ok) return $"Transaction is invalid: {result}";
                return txHash.ToHex();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                return e.Message;
            }
        }

        [JsonRpcMethod("eth_getTransactionReceipt")]
        private JObject GetTransactionReceipt(string txHash)
        {
            var hash = txHash.HexToBytes().ToUInt256();
            var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(hash);

            if (receipt is null)
            {
                return null;
            }

            return ToEthTxFormat(receipt,
                block: _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(receipt.Block));
        }

        [JsonRpcMethod("eth_getTransactionByHash")]
        private JObject GetTransactionByHash(string txHash)
        {
            var hash = txHash.HexToBytes().ToUInt256();
            var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(hash);

            return receipt is null
                ? null
                : ToEthTxFormat(receipt,
                    block: _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(receipt.Block));
        }

        [JsonRpcMethod("eth_sendRawTransaction")]
        private string SendRawTransaction(string rawTx)
        {
            var ethTx = new TransactionChainId(rawTx.HexToBytes());
            var signature = ethTx.Signature.R.Concat(ethTx.Signature.S).Concat(ethTx.Signature.V).ToArray();
            try
            {
                var transaction = new Transaction
                {
                    // this is special case where empty uint160 is allowed
                    To = ethTx.ReceiveAddress?.ToUInt160() ?? new UInt160 {Buffer = ByteString.Empty},
                    Value = ethTx.Value.Reverse().ToArray().ToUInt256(true),
                    From = ethTx.Key.GetPublicAddress().HexToBytes().ToUInt160(),
                    Nonce = Convert.ToUInt64(ethTx.Nonce.ToHex(), 16),
                    GasPrice = Convert.ToUInt64(ethTx.GasPrice.ToHex(), 16),
                    GasLimit = Convert.ToUInt64(ethTx.GasLimit.ToHex(), 16),
                    Invocation = ByteString.CopyFrom(ethTx.Data ?? new byte[] { }),
                };
                if (!ethTx.ChainId.SequenceEqual(new byte[] {TransactionUtils.ChainId}))
                {
                    return "Can not add to transaction pool: BadChainId";
                }

                var result = _transactionPool.Add(transaction, signature.ToSignature());
                if (result != OperatingError.Ok) return $"Can not add to transaction pool: {result}";
                return transaction.FullHash(signature.ToSignature()).ToHex();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                return e.Message;
            }
        }

        [JsonRpcMethod("eth_invokeContract")]
        private JObject InvokeContract(string contract, string sender, string input, ulong gasLimit)
        {
            var contractByHash = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(
                contract.HexToUInt160());
            if (contractByHash is null)
                throw new ArgumentException("Unable to resolve contract by hash (" + contract + ")", nameof(contract));
            if (string.IsNullOrEmpty(input))
                throw new ArgumentException("Invalid input specified", nameof(input));
            if (string.IsNullOrEmpty(sender))
                throw new ArgumentException("Invalid sender specified", nameof(sender));
            var result = _stateManager.SafeContext(() =>
            {
                _stateManager.NewSnapshot();
                var invocationResult = _virtualMachine.InvokeContract(contractByHash,
                    new InvocationContext(sender.HexToUInt160()), input.HexToBytes(), gasLimit);
                _stateManager.Rollback();
                return invocationResult;
            });
            return new JObject
            {
                ["status"] = result.Status.ToString(),
                ["gasLimit"] = gasLimit,
                ["gasUsed"] = result.GasUsed,
                ["ok"] = result.Status == ExecutionStatus.Ok,
                ["result"] = result.ReturnValue?.ToHex() ?? "0x"
            };
        }

        [JsonRpcMethod("eth_estimateGas")]
        private string EstimateGas(JObject args)
        {
            // TODO: gas estimation for contract methods 
            return "0x2dc6c0";
        }

        public static JObject ToEthTxFormat(TransactionReceipt receipt, string? blockHash = null,
            string? blockNumber = null,
            Block? block = null)
        {
            Console.WriteLine(receipt.ToJson());
            return new JObject
            {
                ["transactionHash"] = receipt.Hash.ToHex(),
                ["transactionIndex"] = receipt.IndexInBlock,
                ["blockNumber"] = blockNumber ?? receipt.Block.ToHex(),
                ["blockHash"] = blockHash ?? block.Hash.ToHex(),
                ["cumulativeGasUsed"] = receipt.GasUsed.ToBytes().ToHex(true), // TODO: plus previous
                ["gasUsed"] = receipt.GasUsed.ToBytes().ToHex(true),
                ["contractAddress"] = null,
                ["logs"] = new JArray(),
                ["logsBloom"] =
                    "0x00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
                ["status"] = receipt.Status.CompareTo(TransactionStatus.Executed) == 0 ? "0x1" : "0x0",
                ["value"] = receipt.Transaction.Value.ToBytes().Reverse().ToHex(),
                ["nonce"] = receipt.Transaction.Nonce.ToHex(),
                ["r"] = receipt.Signature.Encode().Take(32).ToHex(),
                ["s"] = receipt.Signature.Encode().Skip(32).Take(32).ToHex(),
                ["v"] = receipt.Signature.Encode().Skip(64).ToHex(),
                ["input"] = receipt.Transaction.Invocation.ToHex(),
                ["gasPrice"] = receipt.Transaction.GasPrice.ToHex(),
                ["gas"] = receipt.Transaction.GasLimit.ToHex(),
                ["hash"] = receipt.Hash.ToHex(),
                ["to"] = receipt.Transaction.To.ToHex(),
                ["from"] = receipt.Transaction.From.ToHex(),
            };
        }
    }
}