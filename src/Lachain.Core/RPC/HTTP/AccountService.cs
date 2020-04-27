using System;
using System.Linq;
using AustinHarris.JsonRpc;
using Google.Protobuf;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Newtonsoft.Json.Linq;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.VM;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Utils;

namespace Lachain.Core.RPC.HTTP
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
            return availableBalance.ToUInt256().ToHex();
        }

        [JsonRpcMethod("verifyRawTransaction")]
        private JObject VerifyRawTransaction(string rawTransaction, string signature)
        {
            var transaction = Transaction.Parser.ParseFrom(rawTransaction.HexToBytes());
            if (!transaction.ToByteArray().SequenceEqual(rawTransaction.HexToBytes()))
                throw new Exception("Failed to validate seiralized and deserialized transactions");
            var s = signature.HexToBytes().ToSignature();
            var txHash = transaction.FullHash(s);
            var json = new JObject {["hash"] = txHash.ToHex()};
            var accepted = new TransactionReceipt
            {
                Transaction = transaction,
                Hash = txHash,
                Signature = s
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
        private JObject SendRawTransaction(string rawTransaction, string signature)
        {
            var transaction = Transaction.Parser.ParseFrom(rawTransaction.HexToBytes());
            var s = signature.HexToBytes().ToSignature();
            var json = new JObject {["hash"] = transaction.FullHash(s).ToHex()};
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