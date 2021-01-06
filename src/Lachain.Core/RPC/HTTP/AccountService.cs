using System;
using System.Linq;
using AustinHarris.JsonRpc;
using Google.Protobuf;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Newtonsoft.Json.Linq;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Serialization;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Core.RPC.HTTP
{
    public class AccountService : JsonRpcService
    {
        private readonly IStateManager _stateManager;
        private readonly ITransactionManager _transactionManager;
        private readonly ITransactionPool _transactionPool;
        private readonly IPrivateWallet _privateWallet;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionSigner _transactionSigner;

        public AccountService(
            IStateManager stateManager,
            ITransactionManager transactionManager,
            ITransactionPool transactionPool,
            IPrivateWallet privateWallet,
            ITransactionBuilder transactionBuilder,
            ITransactionSigner transactionSigner
        )
        {
            _stateManager = stateManager;
            _transactionManager = transactionManager;
            _transactionPool = transactionPool;
            _privateWallet = privateWallet;
            _transactionBuilder = transactionBuilder;
            _transactionSigner = transactionSigner;
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
                throw new Exception("Failed to validate serialized and deserialized transactions");
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

        [JsonRpcMethod("deployContract")]
        private JObject DeployContract(string byteCodeInHex, string input, ulong gasLimit)
        {
            var ecdsaKeyPair = _privateWallet.GetWalletInstance()?.EcdsaKeyPair;
            if (ecdsaKeyPair == null)
                throw new Exception("Wallet is locked");
            if (string.IsNullOrEmpty(byteCodeInHex))
                throw new ArgumentException("Invalid byteCode specified", nameof(byteCodeInHex));
            var from = ecdsaKeyPair.PublicKey.GetAddress();
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            /* calculate contract hash */
            var hash = from.ToBytes().Concat(nonce.ToBytes()).Ripemd();
            Console.WriteLine("Contract Hash: " + hash.ToHex());
            var byteCode = byteCodeInHex.HexToBytes();
            if (!VirtualMachine.VerifyContract(byteCode)) 
                throw new ArgumentException("Unable to validate smart-contract code");
            // TODO: use deploy abi if required
            var tx = _transactionBuilder.DeployTransaction(from, byteCode);
            var signedTx = _transactionSigner.Sign(tx, ecdsaKeyPair);
            var error = _transactionPool.Add(signedTx);
            return new JObject
            {
                ["status"] = signedTx.Status.ToString(),
                ["gasLimit"] = gasLimit,
                ["gasUsed"] = signedTx.GasUsed,
                ["ok"] = error == OperatingError.Ok,
                ["result"] = hash.ToHex()
            };
        }

        [JsonRpcMethod("callContract")]
        private JObject CallContract(string contract, string sender, string input, ulong gasLimit)
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
                var invocationResult = VirtualMachine.InvokeWasmContract(contractByHash,
                    new InvocationContext(sender.HexToUInt160(), snapshot, new TransactionReceipt
                    {
                        // TODO: correctly fill in these fields
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
                ["gasUsed"] = result.GasUsed,
                ["ok"] = result.Status == ExecutionStatus.Ok,
                ["result"] = result.ReturnValue?.ToHex() ?? "0x"
            };
        }
        
        [JsonRpcMethod("sendContract")]
        private JObject SendContract(string contract, string methodSignature, string arguments, ulong gasLimit)
        {
            var ecdsaKeyPair = _privateWallet.GetWalletInstance()?.EcdsaKeyPair;
            if (ecdsaKeyPair == null)
                throw new Exception("Wallet is locked");
            if (string.IsNullOrEmpty(methodSignature))
                throw new ArgumentException("Invalid method signature specified", nameof(methodSignature));
            var contractHash = contract.HexToUInt160();
            var contractByHash = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(contractHash);
            if (contractByHash is null)
                throw new ArgumentException("Unable to resolve contract by hash (" + contract + ")", nameof(contract)); 
            var from = ecdsaKeyPair.PublicKey.GetAddress();
            var tx = _transactionBuilder.InvokeTransaction(
                from,
                contractHash,
                Money.Zero,
                methodSignature,
                ContractEncoder.RestoreTypesFromStrings(arguments.Split(',')));
            var signedTx = _transactionSigner.Sign(tx, ecdsaKeyPair);
            var error = _transactionPool.Add(signedTx);
            return new JObject
            {
                ["status"] = signedTx.Status.ToString(),
                ["gasLimit"] = gasLimit,
                ["gasUsed"] = signedTx.GasUsed,
                ["ok"] = error == OperatingError.Ok,
                ["result"] = signedTx.Hash.ToHex()
            };
        }
    }
}