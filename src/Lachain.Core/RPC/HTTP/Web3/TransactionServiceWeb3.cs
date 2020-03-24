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
using Lachain.Core.Blockchain.OperationManager;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.VM;
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
        private JObject VerifyRawTransaction(string rawTransaction, string signature)
        {
            var transaction = Transaction.Parser.ParseFrom(rawTransaction.HexToBytes());
            if (!transaction.ToByteArray().SequenceEqual(rawTransaction.HexToBytes()))
                throw new Exception("Failed to validate serialized and deserialized transactions");
            var json = new JObject
            {
                ["hash"] = HashUtils.ToHash256(transaction).Buffer.ToHex()
            };
            var accepted = new TransactionReceipt
            {
                Transaction = transaction,
                Hash = HashUtils.ToHash256(transaction),
                Signature = signature.HexToBytes().ToSignature()
            };
            var result = _transactionManager.Verify(accepted);
            json["result"] = result.ToString();
            if (result != OperatingError.Ok)
                json["status"] = false;
            else
                json["status"] = true;
            return json;
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
            return ToEthTxFormat(receipt);
        }

        [JsonRpcMethod("eth_getTransactionByHash")]
        private JObject GetTransactionByHash(string txHash)
        {
            var hash = txHash.HexToBytes().ToUInt256();
            var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(hash);

            if (receipt is null)
            {
                return null;
            } 
            return ToEthTxFormat(receipt);
        }

        [JsonRpcMethod("eth_sendRawTransaction")]
        private string SendRawTransaction(string rawTx)
        {
            var ethTx = new TransactionChainId(rawTx.HexToBytes());
            byte[] signature = ethTx.Signature.R.Concat(ethTx.Signature.S).Concat(ethTx.Signature.V).ToArray();
            try
            {
                var transaction = new Transaction
                {
                    Type = TransactionType.Transfer,
                    To = new UInt160
                    {
                        Buffer = ByteString.CopyFrom(ethTx.ReceiveAddress)
                    },
                    Value = new UInt256
                    {
                        Buffer = ByteString.CopyFrom(ethTx.Value.Reverse().ToArray())
                    },
                    From = new UInt160
                    {
                        Buffer = ByteString.CopyFrom(ethTx.Key.GetPublicAddress().HexToBytes())
                    },
                    Nonce = Convert.ToUInt64(ethTx.Nonce.ToHex(), 16),
                    GasPrice = Convert.ToUInt64(ethTx.GasPrice.ToHex(), 16),
                    GasLimit = Convert.ToUInt64(ethTx.GasLimit.ToHex(), 16),
                };

                var result = _transactionPool.Add(
                    transaction, signature.ToSignature());
                if (result != OperatingError.Ok)
                    return "Can not add to transaction pool";
                return HashUtils.ToHash256(transaction).Buffer.ToHex();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
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

        public static JObject ToEthTxFormat(TransactionReceipt receipt, string blockHash = null, string blockNumber = null)
        {
            Console.WriteLine(receipt.ToJson());
            return new JObject
            {
                ["transactionHash"] = receipt.Hash.ToHex(),
                ["transactionIndex"] = "0x0",
                ["blockNumber"] = blockNumber ?? (receipt.Block == null ? "0x0" : receipt.Block.Buffer.ToHex()),
                ["blockHash"] = blockHash ?? (receipt.Block == null ? "0x0" : receipt.Block.Buffer.ToHex()),
                ["cumulativeGasUsed"] = receipt.GasUsed.ToBytes().ToHex(true), // TODO: plus previous
                ["gasUsed"] = receipt.GasUsed.ToBytes().ToHex(true),
                ["contractAddress"] = null,
                ["logs"] = new JArray(),
                ["logsBloom"] = "0x00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
                ["status"] = receipt.Status.CompareTo(TransactionStatus.Executed) == 0 ? "0x1" : "0x0",
                ["value"] = receipt.Transaction.Value.Buffer.Reverse().ToArray().ToHex(true),
                ["nonce"] = receipt.Transaction.Nonce.ToHex(),
                ["r"] = receipt.Signature.Buffer.ToArray().Take(32).ToArray().ToHex(true),
                ["s"] = receipt.Signature.Buffer.ToArray().Skip(32).Take(32).ToArray().ToHex(true),
                ["v"] = receipt.Signature.Buffer.ToArray().Skip(64).ToArray().ToHex(true),
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