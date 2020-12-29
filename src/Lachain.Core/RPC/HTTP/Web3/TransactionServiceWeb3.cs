using System;
using System.Collections.Generic;
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
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Newtonsoft.Json.Serialization;
using Transaction = Lachain.Proto.Transaction;

namespace Lachain.Core.RPC.HTTP.Web3
{
    public class TransactionServiceWeb3 : JsonRpcService
    {
        private static readonly ILogger<TransactionServiceWeb3> Logger =
            LoggerFactory.GetLoggerForClass<TransactionServiceWeb3>();

        private readonly IStateManager _stateManager;
        private readonly ITransactionManager _transactionManager;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionSigner _transactionSigner;
        private readonly ITransactionPool _transactionPool;
        private readonly IContractRegisterer _contractRegisterer;
        private readonly IPrivateWallet _privateWallet;

        public TransactionServiceWeb3(
            IStateManager stateManager,
            ITransactionManager transactionManager,
            ITransactionBuilder transactionBuilder,
            ITransactionSigner transactionSigner,
            ITransactionPool transactionPool,
            IContractRegisterer contractRegisterer,
            IPrivateWallet privateWallet)
        {
            _stateManager = stateManager;
            _transactionManager = transactionManager;
            _transactionBuilder = transactionBuilder;
            _transactionSigner = transactionSigner;
            _transactionPool = transactionPool;
            _contractRegisterer = contractRegisterer;
            _privateWallet = privateWallet;
        }

        private Transaction MakeTransaction(SignedTransactionBase ethTx)
        {
            return new Transaction
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
        }

        [JsonRpcMethod("eth_verifyRawTransaction")]
        private string VerifyRawTransaction(string rawTx)
        {
            var ethTx = new TransactionChainId(rawTx.HexToBytes());
            var signature = ethTx.Signature.R.Concat(ethTx.Signature.S).Concat(ethTx.Signature.V).ToSignature();
            try
            {
                var transaction = MakeTransaction(ethTx);
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
                Logger.LogError($"Exception in handling eth_verifyRawTransaction: {e}");
                return e.Message;
            }
        }

        [JsonRpcMethod("eth_getTransactionReceipt")]
        private JObject? GetTransactionReceipt(string txHash)
        {
            var hash = txHash.HexToBytes().ToUInt256();
            var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(hash);
            if (receipt is null) return null;
            var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(receipt!.Block);
            if (block is null) return null; // ???
            
            var eventCount = _stateManager.LastApprovedSnapshot.Events.GetTotalTransactionEvents(receipt.Hash);
            var events = new List<Event>();
            for (var i = (uint) 0; i < eventCount; i++)
            {
                var eventLog = _stateManager.LastApprovedSnapshot.Events
                    .GetEventByTransactionHashAndIndex(receipt.Hash, i)!;
                events.Add(eventLog);
            }
            
            return Web3DataFormatUtils.Web3TransactionReceipt(receipt, block!.Hash, receipt.Block, 
                receipt.GasUsed, Web3DataFormatUtils.Web3EventArray(events, receipt!.Block));
        }

        [JsonRpcMethod("eth_getTransactionByHash")]
        private JObject? GetTransactionByHash(string txHash)
        {
            var hash = txHash.HexToUInt256();
            var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(hash);
            if (receipt is null)
                return null;
            var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(receipt!.Block);
            return Web3DataFormatUtils.Web3Transaction(receipt!, block?.Hash, receipt.Block);
        }

        [JsonRpcMethod("eth_getTransactionByBlockHashAndIndex")]
        private JObject? GetTransactionByBlockHashAndIndex(string blockHash, ulong index)
        {
            var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHash(blockHash.HexToUInt256());
            if (block is null)
                return null;
            if ((int)index >= block.TransactionHashes.Count)
                return null;
            var txHash = block.TransactionHashes[(int)index];
            var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(txHash);
            return receipt is null ? null : Web3DataFormatUtils.Web3Transaction(receipt!, block?.Hash, receipt.Block);
        }

        [JsonRpcMethod("eth_getTransactionByBlockNumberAndIndex")]
        private JObject? GetTransactionByBlockNumberAndIndex(string blockTag, ulong index)
        {
            var height = GetBlockNumberByTag(blockTag);
            var block = (height is null) ? null : _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight((ulong) height);
            if (block is null)
                return null;
            if ((int)index >= block.TransactionHashes.Count)
                return null;
            var txHash = block.TransactionHashes[(int)index];
            var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(txHash);
            return receipt is null ? null : Web3DataFormatUtils.Web3Transaction(receipt!, block?.Hash, receipt.Block);
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
                var transaction = MakeTransaction(ethTx);
                if (!ethTx.ChainId.SequenceEqual(new byte[] {TransactionUtils.ChainId}))
                    return "Can not add to transaction pool: BadChainId";
                var result = _transactionPool.Add(transaction, signature.ToSignature());
                if (result != OperatingError.Ok) return $"Can not add to transaction pool: {result}";
                return Web3DataFormatUtils.Web3Data(transaction.FullHash(signature.ToSignature()));
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception in handling eth_sendRawTransaction: {e}");
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

        [JsonRpcMethod("eth_sendTransaction")]
        private string SendTransaction(JObject opts)
        {
            var from = opts["from"];
            var gas = opts["gas"];
            var gasPrice = opts["gasPrice"];
            var data = opts["data"];
            var to = opts["to"];
            var value = opts["value"];
            var nonce = opts["nonce"];
            
            if (to is null) // deploy transaction
            {
                if (data is null)  
                    throw new ArgumentException("To and data fields are both empty");

                // TODO: find other way to access keys to sign txes
                // if (_privateWallet.IsLocked())
                //     throw new MethodAccessException("Wallet is locked");
                _privateWallet.Unlock("12345", 1000);
                var keyPair = _privateWallet.EcdsaKeyPair;
                Logger.LogInformation($"Keys: {keyPair.PublicKey.GetAddress().ToHex()}");

                var byteCode = ((string) data!).HexToBytes();
                if (!VirtualMachine.VerifyContract(byteCode)) 
                    throw new ArgumentException("Unable to validate smart-contract code");
                var fromAddress = ((string) from!).HexToUInt160();
                var nonceToUse = ((ulong) (nonce?? _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(fromAddress)));
                var contractHash = fromAddress.ToBytes().Concat(nonceToUse.ToBytes()).Ripemd();
                Logger.LogInformation($"Contract Hash: {contractHash.ToHex()}");
                var tx = _transactionBuilder.DeployTransaction(fromAddress, byteCode);
                var signedTx = _transactionSigner.Sign(tx, keyPair);
                var error = _transactionPool.Add(signedTx);
                if (error != OperatingError.Ok)
                    throw new ApplicationException($"Can not add to transaction pool: {error}");
                return Web3DataFormatUtils.Web3Data(signedTx.Hash);
            }
            else
            {
                if (data is null) // transfer tx
                {
                    // TODO: implement transfer tx
                    throw new ApplicationException("Not implemented yet");
                }
                else // invoke tx
                {
                    // TODO: implement invoke tx
                    throw new ApplicationException("Not implemented yet");
                }
            }
            
            return "0x";
        }
        
        [JsonRpcMethod("eth_call")]
        private string Call(JObject opts, string? blockId)
        {
            var from = opts["from"];
            var to = opts["to"];
            var data = opts["data"];

            if (to is null && data is null) return "0x";

            var invocation = ((string) data!).HexToBytes();
            var destination = ((string) to!).HexToUInt160();
            var source = from is null ? UInt160Utils.Zero : ((string) from!).HexToUInt160();
            //string from, string to, string gas, string gasPrice, string value, string data
            var contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(destination);
            var systemContract = _contractRegisterer.GetContractByAddress(destination);

            if (contract is null && systemContract is null)
            {
                Logger.LogWarning("Unable to resolve contract by hash (" + contract + ")", nameof(contract));
                return "0x";
            }

            if (!(contract is null))
            {
                InvocationResult result = _stateManager.SafeContext(() =>
                {
                    var snapshot = _stateManager.NewSnapshot();
                    var res = VirtualMachine.InvokeWasmContract(
                        contract,
                        new InvocationContext(source, snapshot, new TransactionReceipt
                        {
                            // TODO: correctly fill these fields
                            Block = snapshot.Blocks.GetTotalBlockHeight(),
                        }),
                        invocation,
                        100_000_000
                    );
                    _stateManager.Rollback();
                    return res;
                });

                return result.ReturnValue?.ToHex() ?? "0x";
            }

            var (err, invocationResult) =
                _InvokeSystemContract(destination, invocation, source, _stateManager.LastApprovedSnapshot);
            if (err != OperatingError.Ok)
            {
                return "0x";
            }

            switch (invocationResult)
            {
                case UInt256 result:
                    return result.ToBytes().ToHex();
                case int result:
                    var res = result.ToHex();
                    while (res.Length < 64)
                        res = "0" + res;
                    return "0x" + res;
                case uint result:
                    var uintHex = result.ToBytes().ToHex(false);
                    while (uintHex.Length < 64)
                        uintHex = "0" + uintHex;
                    return "0x" + uintHex;
                case byte[] result:
                    return result.ToHex(true);
                case bool result:
                    return result ? "0x1" : "0x0";
                case string result:
                    const string start = "0x0000000000000000000000000000000000000000000000000000000000000020";
                    var value = Encoding.ASCII.GetBytes(result).ToHex();
                    var len = (value.Length / 2).ToBytes().ToHex(false);
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
        private string? EstimateGas(JObject opts)
        {
            var gasUsed = GasMetering.DefaultTxCost;
            var from = opts["from"];
            var to = opts["to"];
            var data = opts["data"];

            if (to is null && data is null) return null;

            var invocation = ((string) data!).HexToBytes();
            var destination = ((string) to!).HexToUInt160();
            var source = from is null ? UInt160Utils.Zero : ((string) from!).HexToUInt160();
            gasUsed += (ulong) invocation.Length * GasMetering.InputDataGasPerByte;


            var contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(destination);
            var systemContract = _contractRegisterer.GetContractByAddress(destination);

            if (contract is null && systemContract is null)
            {
                return Web3DataFormatUtils.Web3Number(gasUsed);
            }

            if (!(contract is null))
            {
                InvocationResult invRes = _stateManager.SafeContext(() =>
                {
                    var snapshot = _stateManager.NewSnapshot();
                    var res = VirtualMachine.InvokeWasmContract(
                        contract,
                        new InvocationContext(source, snapshot, new TransactionReceipt
                        {
                            // TODO: correctly fill these fields
                            Block = snapshot.Blocks.GetTotalBlockHeight(),
                        }),
                        invocation,
                        100_000_000
                    );
                    _stateManager.Rollback();
                    return res;
                });
                return invRes.Status == ExecutionStatus.Ok ? 
                    Web3DataFormatUtils.Web3Number(gasUsed + invRes.GasUsed) : "0x";
            }


            InvocationResult systemContractInvRes = _stateManager.SafeContext(() =>
            {
                var snapshot = _stateManager.NewSnapshot();
                var systemContractContext = new InvocationContext(source, snapshot, new TransactionReceipt
                {
                    Block = snapshot.Blocks.GetTotalBlockHeight(),
                });
                var invocationResult =
                    ContractInvoker.Invoke(destination, systemContractContext, invocation, 100_000_000);
                _stateManager.Rollback();

                return invocationResult;
            });
            return systemContractInvRes.Status == ExecutionStatus.Ok
                ? (gasUsed + systemContractInvRes.GasUsed).ToHex()
                : "0x";
        }

        [JsonRpcMethod("eth_gasPrice")]
        private string GetNetworkGasPrice()
        {
            return Web3DataFormatUtils.Web3Number(_stateManager.CurrentSnapshot.NetworkGasPrice.ToUInt256());
        }

        [JsonRpcMethod("eth_signTransaction")]
        private string SignTransaction(JObject opts)
        {
            // TODO: implement tx signing
            throw new ApplicationException("Not implemented yet");
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

                invResult = result.ReturnValue!;
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
        
        private ulong? GetBlockNumberByTag(string blockTag)
        {
            return blockTag switch
            {
                "latest" => _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight(),
                "earliest" => 0,
                "pending" => null,
                _ => blockTag.HexToUlong()
            };
        }

    }
}