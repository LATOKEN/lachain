using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using AustinHarris.JsonRpc;
using Google.Protobuf;
using Newtonsoft.Json.Linq;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.VM;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Transaction = Lachain.Proto.Transaction;

namespace Lachain.Core.RPC.HTTP.Web3
{
    public class TransactionServiceWeb3 : JsonRpcService
    {
        private readonly IStateManager _stateManager;
        private readonly ITransactionManager _transactionManager;
        private readonly ITransactionPool _transactionPool;
        private readonly IContractRegisterer _contractRegisterer;

        public TransactionServiceWeb3(
            IStateManager stateManager,
            ITransactionManager transactionManager,
            ITransactionPool transactionPool,
            IContractRegisterer contractRegisterer)
        {
            _stateManager = stateManager;
            _transactionManager = transactionManager;
            _transactionPool = transactionPool;
            _contractRegisterer = contractRegisterer;
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
        private JObject? GetTransactionReceipt(string txHash)
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
        private JObject? GetTransactionByHash(string txHash)
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
            
            var r = ethTx.Signature.R;
            while (r.Length < 32)
                r = "00".HexToBytes().Concat(r).ToArray();
            
            var s = ethTx.Signature.S;
            while (s.Length < 32)
                s = "00".HexToBytes().Concat(s).ToArray();
            
            var signature = r.Concat(s).Concat(ethTx.Signature.V).ToArray();
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
                    Invocation = ethTx.Data is null ? ByteString.Empty : ByteString.CopyFrom(ethTx.Data),
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
                var snapshot = _stateManager.NewSnapshot();
                var invocationResult = VirtualMachine.InvokeWasmContract(
                    contractByHash,
                    new InvocationContext(sender.HexToUInt160(), snapshot, new TransactionReceipt
                    {
                        // TODO: correctly fill these fields
                        Block = snapshot.Blocks.GetTotalBlockHeight(),
                    }),
                    input.HexToBytes(),
                    gasLimit
                );
                _stateManager.Rollback();
                return invocationResult;
            });
            return new JObject
            {
                ["status"] = result.Status.ToString(),
                ["gasLimit"] = gasLimit,
                ["gasUsed"] = result.GasUsed.ToHex(),
                ["ok"] = result.Status == ExecutionStatus.Ok,
                ["result"] = result.ReturnValue?.ToHex() ?? "0x"
            };
        }

        [JsonRpcMethod("eth_call")]
        private string Call(JObject opts, string? blockId)
        {
            string from = (string)opts["from"];
            string to = (string)opts["to"];
            string data = (string)opts["data"];
            //string from, string to, string gas, string gasPrice, string value, string data
            var contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(
                to.HexToUInt160());
            var systemContract= _contractRegisterer.GetContractByAddress(to.HexToUInt160());
            if (contract is null && systemContract is null)
                throw new ArgumentException("Unable to resolve contract by hash (" + contract + ")", nameof(contract));
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException("Invalid input specified", nameof(data));
            if (string.IsNullOrEmpty(from))
                from = UInt160Utils.Zero.ToHex();

            if (contract != null)
            {
                InvocationResult result = _stateManager.SafeContext(() =>
                {
                    var snapshot = _stateManager.NewSnapshot();
                    var invocationResult = VirtualMachine.InvokeWasmContract(
                        contract,
                        new InvocationContext(from.HexToUInt160(), snapshot, new TransactionReceipt
                        {
                            // TODO: correctly fill these fields
                            Block = snapshot.Blocks.GetTotalBlockHeight(),
                        }),
                        data.HexToBytes(),
                        100_000_000
                    );
                    _stateManager.Rollback();
                    return invocationResult;
                });

                return result.ReturnValue?.ToHex() ?? "0x";
            }
            
            var (err, invocationResult) = _InvokeSystemContract(to.HexToUInt160(), data.HexToBytes(), from.HexToUInt160(), _stateManager.LastApprovedSnapshot);
            if (err != OperatingError.Ok)
            {
                return "0x";
            }

            switch (invocationResult)
            {
                case UInt256 result:
                    return result.ToHex();
                case int result:
                    var res = result.ToHex();
                    while (res.Length < 64)
                        res = "0" + res;
                    return "0x" + res;
                case uint result:
                    var uintHex = result.ToBytes().Reverse().ToHex(false);
                    while (uintHex.Length < 64)
                        uintHex = "0" + uintHex;
                    return "0x" + uintHex;
                case byte[] result:
                    return result.ToHex(true);
                case bool result:
                    return result ? "0x1" : "0x0";
                case string result:
                    const string start = "0x0000000000000000000000000000000000000000000000000000000000000020";
                    var value= Encoding.ASCII.GetBytes(result).ToHex();
                    var len = (value.Length / 2).ToBytes().Reverse().ToHex(false);
                    while (len.Length < 64)
                        len = '0' + len;
                    while (value.Length < 64)
                        value += '0';
                    return start + len + value;
                default:
                    return "0x";
            }
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
            // Console.WriteLine(receipt.ToJson());
            return new JObject
            {
                ["transactionHash"] = receipt.Hash.ToHex(),
                ["transactionIndex"] = receipt.IndexInBlock,
                ["blockNumber"] = blockNumber ?? receipt.Block.ToHex(),
                ["blockHash"] = blockHash ?? block?.Hash.ToHex(),
                ["cumulativeGasUsed"] = receipt.GasUsed.ToBytes().Reverse().ToHex(), // TODO: plus previous
                ["gasUsed"] = receipt.GasUsed.ToBytes().Reverse().ToHex(),
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
        
        

        private (OperatingError, object?) _InvokeSystemContract(
            UInt160 address, byte[] invocation, UInt160 from, IBlockchainSnapshot snapshot
        )
        {
            object invResult;
            try
            {
                var context = new InvocationContext(from, snapshot, new TransactionReceipt
                {
                    // TODO: correctly fill these fields
                    Block = snapshot.Blocks.GetTotalBlockHeight(),
                });
                var call = _contractRegisterer.DecodeContract(context, address, invocation);
                if (call is null) return (OperatingError.ContractFailed, null);
                
                var result = VirtualMachine.InvokeSystemContract(call, context, invocation, 100_000_000);
                
                invResult = result.ReturnValue;
            }
            catch (Exception e) when (
                e is NotSupportedException ||
                e is InvalidOperationException ||
                e is TargetInvocationException ||
                e is ContractAbiException
            )
            {
                return (OperatingError.ContractFailed, null);
            }

            return (OperatingError.Ok, invResult);
        }
    }
}