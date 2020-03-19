using System;
using System.Globalization;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using AustinHarris.JsonRpc;
using Google.Protobuf;
using Newtonsoft.Json.Linq;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.Pool;
using Phorkus.Core.VM;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility.Utils;
using Transaction = Phorkus.Proto.Transaction;

namespace Phorkus.Core.RPC.HTTP.Web3
{
    public class AccountServiceWeb3 : JsonRpcService
    {
        private readonly IVirtualMachine _virtualMachine;
        private readonly IStateManager _stateManager;
        private readonly ITransactionManager _transactionManager;
        private readonly ITransactionPool _transactionPool;

        public AccountServiceWeb3(
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

        [JsonRpcMethod("eth_getBalance")]
        private string GetBalance(string address, string tag)
        {
            var addressUint160 = address.HexToBytes().ToUInt160();
            var availableBalance =
                _stateManager.LastApprovedSnapshot.Balances.GetBalance(addressUint160);
            return availableBalance.ToWei().ToUInt160().ToHex().HexToBytes().Reverse()
                // .SkipWhile(b => b == 0)
                .ToHex();
        }

        [JsonRpcMethod("eth_verifyRawTransaction")]
        private JObject VerifyRawTransaction(string rawTransation, string signature)
        {
            var transaction = Transaction.Parser.ParseFrom(rawTransation.HexToBytes());
            if (!transaction.ToByteArray().SequenceEqual(rawTransation.HexToBytes()))
                throw new Exception("Failed to validate seiralized and deserialized transactions");
            var json = new JObject
            {
                ["hash"] = transaction.ToHash256().Buffer.ToHex()
            };
            var accepted = new TransactionReceipt
            {
                Transaction = transaction,
                Hash = transaction.ToHash256(),
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

        [JsonRpcMethod("eth_sendRawTransaction")]
        private string SendRawTransaction(string rawTx)
        {
            var ethTx = new TransactionChainId(rawTx.HexToByteArray());
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
                        Buffer = ByteString.CopyFrom(ethTx.Value)
                    },
                    From = new UInt160
                    {
                        Buffer = ByteString.CopyFrom(ethTx.Key.GetPublicAddress().HexToBytes())
                    },
                    Nonce = ulong.Parse(ethTx.Nonce.ToHex(), NumberStyles.HexNumber),
                    GasPrice = ulong.Parse(ethTx.GasPrice.ToHex(), NumberStyles.HexNumber),
                    GasLimit = ulong.Parse(ethTx.GasLimit.ToHex(), NumberStyles.HexNumber),
                };

                Console.WriteLine(transaction);
                
                
                var result = _transactionPool.Add(
                    transaction, rawTx.HexToBytes().ToSignature());
                if (result != OperatingError.Ok)
                    return "Can not add to transaction pool";
                return transaction.ToHash256().Buffer.ToHex();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return e.Message;
            }
        }

        [JsonRpcMethod("eth_getTransactionCount")]
        private ulong GetTransactionCount(string from, string blockId)
        {
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(
                from.HexToBytes().ToUInt160());
            return nonce;
        }
        private ulong GetTransactionCount(string from)
        {
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(
                from.HexToBytes().ToUInt160());
            return nonce;
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
        
        [JsonRpcMethod("eth_getCode")]
        private string GetCode(string contract, string blockId)
        {
            var contractByHash = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(
                contract.HexToUInt160());
            
            return contractByHash is null ? "0x" : "0x1";
        }
    }
}