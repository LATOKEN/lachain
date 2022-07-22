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
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
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
using System.Threading.Tasks;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Config;
using Lachain.Utility;

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

        public Transaction MakeTransaction(SignedTransactionBase ethTx)
        {
            return new Transaction
            {
                // this is special case where empty uint160 is allowed
                To = ethTx.ReceiveAddress?.ToUInt160() ?? UInt160Utils.Empty,
                Value = ethTx.Value.ToUInt256(true),
                From = ethTx.Key.GetPublicAddress().HexToBytes().ToUInt160(),
                Nonce = Convert.ToUInt64(ethTx.Nonce.ToHex(), 16),
                GasPrice = Convert.ToUInt64(ethTx.GasPrice.ToHex(), 16),
                GasLimit = Convert.ToUInt64(ethTx.GasLimit.ToHex(), 16),
                Invocation = ethTx.Data is null ? ByteString.Empty : ByteString.CopyFrom(ethTx.Data),
            };
        }

        [JsonRpcMethod("eth_verifyRawTransaction")]
        public string VerifyRawTransaction(string rawTx)
        {
            var ethTx = new TransactionChainId(rawTx.HexToBytes());
            var useNewChainId =
                HardforkHeights.IsHardfork_9Active(_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight() + 1);
            var signature = ethTx.Signature.R.Concat(ethTx.Signature.S).Concat(ethTx.Signature.V).ToSignature(useNewChainId);
            try
            {
                var transaction = MakeTransaction(ethTx);
                var txHash = transaction.FullHash(signature, useNewChainId);
                var result = _transactionManager.Verify(new TransactionReceipt
                {
                    Hash = txHash,
                    Signature = signature,
                    Status = TransactionStatus.Pool,
                    Transaction = transaction
                }, useNewChainId);

                if (result != OperatingError.Ok) throw new Exception($"Transaction is invalid: {result}");
                return txHash.ToHex();
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception in handling eth_verifyRawTransaction: {e}");
                throw;
            }
        }

        [JsonRpcMethod("eth_getTransactionReceipt")]
        public JObject? GetTransactionReceipt(string txHash)
        {
            var hash = txHash.HexToBytes().ToUInt256();
            var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(hash);
            if (receipt is null) return null;
            var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(receipt!.Block);
            if (block is null) return null; // ???
            
            var eventCount = _stateManager.LastApprovedSnapshot.Events.GetTotalTransactionEvents(receipt.Hash);
            var events = new List<EventObject>();
            for (var i = (uint) 0; i < eventCount; i++)
            {
                var eventLog = _stateManager.LastApprovedSnapshot.Events
                    .GetEventByTransactionHashAndIndex(receipt.Hash, i)!;
                if(eventLog.Event is null) continue;
                events.Add(eventLog);
            }
            
            return Web3DataFormatUtils.Web3TransactionReceipt(receipt, block!.Hash, receipt.Block, 
                receipt.GasUsed, Web3DataFormatUtils.Web3EventArray(events, receipt!.Block, block!.Hash));
        }

        [JsonRpcMethod("eth_getTransactionByHash")]
        public JObject? GetTransactionByHash(string txHash)
        {
            var hash = txHash.HexToUInt256();
            var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(hash);

            if (receipt is null)
            {
                receipt = _transactionPool.GetByHash(hash);
                if(receipt is null)
                {
                    return null;
                }
                return Web3DataFormatUtils.Web3Transaction(receipt!);
            }
            var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(receipt!.Block);
            return Web3DataFormatUtils.Web3Transaction(receipt!, block?.Hash, receipt.Block);
        }

        [JsonRpcMethod("eth_getTransactionByBlockHashAndIndex")]
        public JObject? GetTransactionByBlockHashAndIndex(string blockHash, ulong index)
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
        public JObject? GetTransactionByBlockNumberAndIndex(string blockTag, ulong index)
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
        public string SendRawTransaction(string rawTx)
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
                bool useNewChainId =
                    HardforkHeights.IsHardfork_9Active(_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight() + 1);
                if (!ethTx.ChainId.SequenceEqual(new byte[] {(byte)(TransactionUtils.ChainId(useNewChainId))}))
                    throw new Exception($"Can not add to transaction pool: BadChainId");
                var result = _transactionPool.Add(transaction, signature.ToSignature(useNewChainId));
                if (result != OperatingError.Ok) throw new Exception($"Can not add to transaction pool: {result}");
                return Web3DataFormatUtils.Web3Data(transaction.FullHash(signature.ToSignature(useNewChainId), useNewChainId));
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception in handling eth_sendRawTransaction: {e}");
                throw;
            }
        }

        [JsonRpcMethod("la_sendRawTransactionBatch")]
        public List<string> SendRawTransactionBatch(List<string> rawTxs)
        {
            List<string> txIds = new List<string>();

            Logger.LogInformation($"Received raw transaction strings count:: {rawTxs.Count}");

            var time = DateTime.Now;

            foreach (string rawTx in rawTxs)
            {
                txIds.Add(SendRawTransaction(rawTx));
            }

            Logger.LogInformation($"Response count:: {txIds.Count}");
            Logger.LogInformation($"Time taken for series:: {DateTime.Now - time}");

            return txIds;
        }

        [JsonRpcMethod("la_sendRawTransactionBatchParallel")]
         public List<string> SendRawTransactionBatchParallel(List<string> rawTxs)
         {
            List<string> txIds = new List<string>();
 
            Logger.LogInformation($"Received raw transaction strings count:: {rawTxs.Count}");

            var time = DateTime.Now;

            Parallel.For(0, rawTxs.Count, i => {
                 txIds.Add(SendRawTransaction(rawTxs[i]));
             });
 
            Logger.LogInformation($"Response count:: {txIds.Count}");
            Logger.LogInformation($"Time taken for Parallel:: {DateTime.Now - time}");

            return txIds;
         }

        [JsonRpcMethod("eth_invokeContract")]
        private JObject InvokeContract(string contract, string sender, string input, ulong gasLimit)
        {
            var contractByHash = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(
                contract.HexToUInt160());
            if (contractByHash is null)
            {
                return new JObject();
                //throw new ArgumentException("Unable to resolve contract by hash (" + contract + ")", nameof(contract));
            }

            if (string.IsNullOrEmpty(input))
            {
                return new JObject();
                //throw new ArgumentException("Invalid input specified", nameof(input));
            }

            if (string.IsNullOrEmpty(sender))
            {
                return new JObject();
                //throw new ArgumentException("Invalid sender specified", nameof(sender));
            }

            var result = _stateManager.SafeContext(() =>
            {
                var snapshot = _stateManager.NewSnapshot();
                var invocationResult = VirtualMachine.InvokeWasmContract(
                    contractByHash,
                    new InvocationContext(sender.HexToUInt160(), snapshot, new TransactionReceipt
                    {
                        Block = snapshot.Blocks.GetTotalBlockHeight(),
                        Transaction = new Transaction{Value = 0.ToUInt256()}
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
            var signedTx = MakeAndSignTransaction(opts);
            var error = _transactionPool.Add(signedTx);
            if (error != OperatingError.Ok)
            {
                throw new ApplicationException($"Can not add to transaction pool: {error}");
            }

            return Web3DataFormatUtils.Web3Data(signedTx.Hash);
        }
        
        [JsonRpcMethod("eth_call")]
        public string Call(JObject opts, string? blockId)
        {
            var from = opts["from"];
            if (from is null)
                opts["from"] = UInt160Utils.Zero.ToHex();
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
                throw new Exception($"Unable to resolve contract by hash {contract}");
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
                            Block = snapshot.Blocks.GetTotalBlockHeight(),
                            Transaction = MakeTransaction(opts)
                        }),
                        invocation,
                        GasMetering.DefaultBlockGasLimit
                    );
                    _stateManager.Rollback();
                    return res;
                });

                return result.ReturnValue?.ToHex(true) ?? throw new Exception("Invalid return value from contract call");;
            }

            var (err, invocationResult) =
                _InvokeSystemContract(destination, invocation, source, _stateManager.LastApprovedSnapshot);
            if (err != OperatingError.Ok)
            {
                throw new Exception("Error in system contract call");;
            }

            switch (invocationResult)
            {
                case UInt256 result:
                    return result.ToBytes().ToHex(true);
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
        public string? EstimateGas(JObject opts)
        {
            Logger.LogInformation($"eth_estimateGas({opts})");
            var gasUsed = GasMetering.DefaultTxCost;
            var from = opts["from"];
            var to = opts["to"];
            var data = opts["data"];
        
            if (to is null && data is null) 
                throw new ArgumentException("To and data fields are both empty");;
        
            var invocation = ((string) data!).HexToBytes();
            var destination = to is null ? UInt160Utils.Zero : ((string) to!).HexToUInt160();
            var source = from is null ? UInt160Utils.Zero : ((string) from!).HexToUInt160();
            gasUsed += (ulong) invocation.Length * GasMetering.InputDataGasPerByte;

            Transaction tx = MakeTransaction(opts);
        
            if (to is null) // deploy contract
            {
                if (!VirtualMachine.VerifyContract(invocation,
                        HardforkHeights.IsHardfork_2Active(_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()))) 
                    throw new ArgumentException("Unable to validate smart-contract code");
                InvocationResult invRes = _stateManager.SafeContext(() =>
                {
                    var snapshot = _stateManager.NewSnapshot();
                    var context = new InvocationContext(source, snapshot, new TransactionReceipt
                    {
                        Block = snapshot.Blocks.GetTotalBlockHeight(),
                        Transaction = tx
                    });
                    var abi = ContractEncoder.Encode(DeployInterface.MethodDeploy, invocation);
                    var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.DeployContract, abi);
                    if (call is null)
                        throw new Exception("Failed to create system call");
                    var res = VirtualMachine.InvokeSystemContract(
                        call,
                        context,
                        invocation,
                        GasMetering.DefaultBlockGasLimit
                    );
                    _stateManager.Rollback();
                    return res;
                });
                return invRes.Status == ExecutionStatus.Ok
                    ? Web3DataFormatUtils.Web3Number(gasUsed + invRes.GasUsed)
                    : throw new Exception("Error in contract call");
            }
            
            var contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(destination);
            var systemContract = _contractRegisterer.GetContractByAddress(destination);
        
            if (contract is null && systemContract is null)
            {
                InvocationResult transferInvRes = _stateManager.SafeContext(() =>
                {
                    var snapshot = _stateManager.NewSnapshot();
                    var systemContractContext = new InvocationContext(source, snapshot, new TransactionReceipt
                    {
                        Block = snapshot.Blocks.GetTotalBlockHeight(),
                        Transaction = tx
                    });
                
                    var localInvocation = ContractEncoder.Encode("transfer(address,uint256)", source, 0.ToUInt256());
                    var invocationResult =
                        ContractInvoker.Invoke(ContractRegisterer.LatokenContract, systemContractContext, localInvocation, GasMetering.DefaultBlockGasLimit);
                    _stateManager.Rollback();
        
                    return invocationResult;
                });
        
                return transferInvRes.Status == ExecutionStatus.Ok
                    ? (gasUsed + transferInvRes.GasUsed).ToHex()
                    : Web3DataFormatUtils.Web3Number(gasUsed);
            }
        
            if (!(contract is null))
            {
                InvocationResult invRes = _stateManager.SafeContext(() =>
                {
                    var snapshot = _stateManager.NewSnapshot();
					if (!tx.Value.IsZero())
					{
                        var transferContext = new InvocationContext(source, snapshot, new TransactionReceipt
                        {
                            Block = snapshot.Blocks.GetTotalBlockHeight(),
                            Transaction = tx
                        });
                    
                        var localInvocation = ContractEncoder.Encode("transfer(address,uint256)", destination, tx.Value);
                        var transferResult =
                            ContractInvoker.Invoke(ContractRegisterer.LatokenContract, transferContext, localInvocation, GasMetering.DefaultBlockGasLimit);
                        gasUsed += transferResult.GasUsed;
                    }
                    var res = VirtualMachine.InvokeWasmContract(
                        contract,
                        new InvocationContext(source, snapshot, new TransactionReceipt
                        {
                            Block = snapshot.Blocks.GetTotalBlockHeight(),
                            Transaction = tx
                        }),
                        invocation,
                        GasMetering.DefaultBlockGasLimit
                    );
                    _stateManager.Rollback();
                    return res;
                });
                return invRes.Status == ExecutionStatus.Ok ? 
                    Web3DataFormatUtils.Web3Number(gasUsed + invRes.GasUsed) 
                    : throw new Exception("Error in contract call");
            }
        
            InvocationResult systemContractInvRes = _stateManager.SafeContext(() =>
            {
                var snapshot = _stateManager.NewSnapshot();
                if (!tx.Value.IsZero())
                {
                    var transferContext = new InvocationContext(source, snapshot, new TransactionReceipt
                    {
                        Block = snapshot.Blocks.GetTotalBlockHeight(),
                        Transaction = tx
                    });
                
                    var localInvocation = ContractEncoder.Encode("transfer(address,uint256)", destination, tx.Value);
                    var transferResult =
                        ContractInvoker.Invoke(ContractRegisterer.LatokenContract, transferContext, localInvocation, GasMetering.DefaultBlockGasLimit);
                    gasUsed += transferResult.GasUsed;
                }
                var systemContractContext = new InvocationContext(source, snapshot, new TransactionReceipt
                {
                    Block = snapshot.Blocks.GetTotalBlockHeight(),
                    Transaction = tx
                });
                
                var invocationResult =
                    ContractInvoker.Invoke(destination, systemContractContext, invocation, GasMetering.DefaultBlockGasLimit);
                _stateManager.Rollback();
        
                return invocationResult;
            });
        
            return systemContractInvRes.Status == ExecutionStatus.Ok
                ? (gasUsed + systemContractInvRes.GasUsed).ToHex()
                : throw new Exception("Error in contract call");
        }

        [JsonRpcMethod("eth_gasPrice")]
        public string GetNetworkGasPrice()
        {
            Logger.LogInformation("eth_gasPrice API called:: ");

            return Web3DataFormatUtils.Web3Number(_stateManager.CurrentSnapshot.NetworkGasPrice.ToUInt256());
        }

        [JsonRpcMethod("eth_signTransaction")]
        private string SignTransaction(JObject opts)
        {
            var signedTx = MakeAndSignTransaction(opts);
            var transaction = signedTx.Transaction;
            var sign = signedTx.Signature;
            var rawTx = transaction.RlpWithSignature(sign, HardforkHeights.IsHardfork_9Active(_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight() + 1));
            return Web3DataFormatUtils.Web3Data(rawTx);
        }

        private TransactionReceipt MakeAndSignTransaction(JObject opts){

            var from = opts["from"];
            var gas = opts["gas"];
            var gasPrice = opts["gasPrice"];
            var data = opts["data"];
            var to = opts["to"];
            var value = opts["value"];
            var nonce = opts["nonce"];

            if(from is null){
                throw new ArgumentException("from should not be null");
            }
            var fromAddress = ((string) from!).HexToUInt160();


            ulong? nonceToUse = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(fromAddress);
            if(!(nonce is null)) nonceToUse = ((string)nonce!).HexToUlong();

            ulong? gasToUse = null;
            if(!(gas is null)) gasToUse = ((string)gas!).HexToUlong();
            
            ulong? gasPriceToUse = null;
            if(!(gasPrice is null)) gasPriceToUse  = ((string)gasPrice!).HexToUlong();
            
            byte[]? byteCode = null;
            if(!(data is null)) byteCode = ((string) data!).HexToBytes();
            
            
            if(_privateWallet.IsLocked()) throw new Exception("wallet is locked");
            var keyPair = _privateWallet.EcdsaKeyPair;
            Logger.LogInformation($"Keys: {keyPair.PublicKey.GetAddress().ToHex()}");
            

            Transaction tx;

            if (to is null) // deploy transaction
            {
                if (data is null)
                {
                    throw new ArgumentException("To and data fields are both empty");
                }

                if (!VirtualMachine.VerifyContract(byteCode!, 
                        HardforkHeights.IsHardfork_2Active(_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()))) 
                    throw new ArgumentException("Unable to validate smart-contract code");
                
                
                var contractHash = fromAddress.ToBytes().Concat(((ulong)nonceToUse).ToBytes()).Ripemd();
                Logger.LogInformation($"Contract Hash: {contractHash.ToHex()}");
                tx = _transactionBuilder.DeployTransaction(fromAddress, byteCode!, gasToUse, gasPriceToUse , nonceToUse);
                
            }
            else
            {

                if((value is null) && (data is null)) throw new ArgumentException("value and data both null");
                var toAddress = ((string)to!).HexToUInt160();
                var valueToUse = UInt256Utils.Zero.ToMoney();
                if(!(value is null)) valueToUse = ((string)value!).HexToBytes().ToUInt256(true).ToMoney();
                tx = _transactionBuilder.TransferTransaction(fromAddress , toAddress , valueToUse , gasToUse, gasPriceToUse, nonceToUse, byteCode);
               
            }

            return _transactionSigner.Sign(tx, keyPair, 
                HardforkHeights.IsHardfork_9Active(_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight() + 1));
        }

        private Transaction MakeTransaction(JObject opts)
        {
            var from = opts["from"];
            var gas = opts["gas"];
            var gasPrice = opts["gasPrice"];
            var data = opts["data"];
            var to = opts["to"];
            var value = opts["value"];
            var nonce = opts["nonce"];

            if(from is null){
                throw new ArgumentException("from should not be null");
            }
            var fromAddress = ((string) from!).HexToUInt160();

            ulong? nonceToUse = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(fromAddress);
            if(!(nonce is null)) nonceToUse = ((string)nonce!).HexToUlong();

            ulong? gasToUse = null;
            if(!(gas is null)) gasToUse = ((string)gas!).HexToUlong();
            
            ulong? gasPriceToUse = null;
            if(!(gasPrice is null)) gasPriceToUse  = ((string)gasPrice!).HexToUlong();
            
            byte[]? byteCode = null;
            if(!(data is null)) byteCode = ((string) data!).HexToBytes();

            if (to is null) // deploy transaction
            {
                if (data is null)
                {
                    throw new ArgumentException("To and data fields are both empty");
                }

                if (!VirtualMachine.VerifyContract(byteCode!, 
                        HardforkHeights.IsHardfork_2Active(_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()))) 
                    throw new ArgumentException("Unable to validate smart-contract code");
                
                
                var contractHash = fromAddress.ToBytes().Concat(((ulong)nonceToUse).ToBytes()).Ripemd();
                Logger.LogInformation($"Contract Hash: {contractHash.ToHex()}");
                return _transactionBuilder.DeployTransaction(fromAddress, byteCode!, gasToUse, gasPriceToUse , nonceToUse);
            }
            if((value is null) && (data is null)) throw new ArgumentException("value and data both null");
            var toAddress = ((string)to!).HexToUInt160();
            var valueToUse = UInt256Utils.Zero.ToMoney();
            if(!(value is null)) valueToUse = HexUtils.ToEvenBytesCount((string)value!).HexToBytes().ToUInt256(true).ToMoney();
            return _transactionBuilder.TransferTransaction(fromAddress , toAddress , valueToUse , gasToUse, gasPriceToUse, nonceToUse, byteCode);
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
                    Block = snapshot.Blocks.GetTotalBlockHeight(),
                    Transaction = new Transaction{Value = 0.ToUInt256()}
                });
                var call = _contractRegisterer.DecodeContract(context, address, invocation);
                if (call is null) return (OperatingError.ContractFailed, null);

                var result = VirtualMachine.InvokeSystemContract(call, context, invocation, GasMetering.DefaultBlockGasLimit);

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
