using System;
using System.Linq;
using AustinHarris.JsonRpc;
using Google.Protobuf;
using Newtonsoft.Json.Linq;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Utils;
using Phorkus.Core.VM;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.RPC.HTTP
{
    public class AccountService : JsonRpcService
    {
        private readonly IVirtualMachine _virtualMachine;
        private readonly IStateManager _stateManager;
        private readonly ITransactionManager _transactionManager;
        private readonly ITransactionPool _transactionPool;

        public AccountService(
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

        [JsonRpcMethod("getBalance")]
        private string GetBalance(string address)
        {
            var addressUint160 = address.HexToBytes().ToUInt160();
            var availableBalance =
                _stateManager.LastApprovedSnapshot.Balances.GetBalance(addressUint160);
            return availableBalance.ToUInt256().Buffer.ToHex();
        }

        [JsonRpcMethod("verifyRawTransaction")]
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

        [JsonRpcMethod("sendRawTransaction")]
        private JObject SendRawTransaction(string rawTransation, string signature)
        {
            var transaction = Transaction.Parser.ParseFrom(rawTransation.HexToBytes());
            var json = new JObject
            {
                ["hash"] = transaction.ToHash256().Buffer.ToHex()
            };
            var result = _transactionPool.Add(
                transaction, signature.HexToBytes().ToSignature());
            if (result != OperatingError.Ok)
                json["failed"] = true;
            else
                json["failed"] = false;
            json["result"] = result.ToString();
            return json;
        }

        [JsonRpcMethod("getTotalTransactionCount")]
        private ulong GetTotalTransactionCount(string from)
        {
            var result = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(
                from.HexToBytes().ToUInt160());
            return result;
        }
        
        [JsonRpcMethod("invokeContract")]
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
    }
}