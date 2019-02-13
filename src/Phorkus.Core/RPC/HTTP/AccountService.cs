using System;
using System.Linq;
using AustinHarris.JsonRpc;
using Google.Protobuf;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Utils;
using Phorkus.Core.VM;
using Phorkus.Proto;
using Phorkus.Storage.State;
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
        
        [JsonRpcMethod("getAvailableAssets")]
        private JObject GetAvailableAssets()
        {
            var assetRepository = _stateManager.LastApprovedSnapshot.Assets;
            var assetHashesByName = _stateManager.LastApprovedSnapshot.Assets.GetAssetHashes()
                .Select(assetHash => assetRepository.GetAssetByHash(assetHash));
            var json = new JObject();
            foreach (var asset in assetHashesByName)
            {
                if (asset is null)
                    continue;
                json[asset.Name] = asset.Hash.Buffer.ToHex();
            }
            return json;
        }
        
        [JsonRpcMethod("getBalance")]
        private JObject GetBalance(string address, string assetHash)
        {
            var addressUint160 = address.HexToBytes().ToUInt160();
            var assetUint160 = assetHash.HexToBytes().ToUInt160();
            var availableBalance =
                _stateManager.LastApprovedSnapshot.Balances.GetAvailableBalance(addressUint160, assetUint160);
            var withdrawingBalance =
                _stateManager.LastApprovedSnapshot.Balances.GetWithdrawingBalance(addressUint160, assetUint160);
            var json = new JObject
            {
                ["available"] = availableBalance.ToUInt256().Buffer.ToHex(),
                ["withdrawing"] = withdrawingBalance.ToUInt256().Buffer.ToHex()
            };
            return json;
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
            var accepted = new AcceptedTransaction
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
        private JObject InvokeContract(string contract, string sender, string input)
        {
            var contractByHash = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(
                contract.HexToUInt160());
            if (contractByHash is null)
                throw new ArgumentException("Unable to resolve contract by hash (" + contract + ")", nameof(contract));
            if (string.IsNullOrEmpty(input))
                throw new ArgumentException("Invalid input specified", nameof(input));
            if (string.IsNullOrEmpty(sender))
                throw new ArgumentException("Invalid sender specified", nameof(sender));
            _stateManager.NewSnapshot();
            var status = _virtualMachine.InvokeContract(contractByHash, sender.HexToUInt160(), input.HexToBytes(), out var returnValue);
            _stateManager.Rollback();
            var hex = returnValue?.ToHex();
            return new JObject
            {
                ["status"] = status.ToString(),
                ["ok"] = status == ExecutionStatus.Ok,
                ["result"] = hex ?? "0x"
            };
        }
    }
}