using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Utils;
using Phorkus.Core.VM;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.CLI
{
    public class ConsoleCommands : IConsoleCommands
    {
        private const uint AddressLength = 20;
        private const uint TxLength = 32;

        private readonly IValidatorManager _validatorManager;
        private readonly ITransactionPool _transactionPool;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly ICrypto _crypto;
        private readonly IStateManager _stateManager;
        private readonly KeyPair _keyPair;
        private readonly IVirtualMachine _virtualMachine;

        public ConsoleCommands(
            ITransactionBuilder transactionBuilder,
            ITransactionPool transactionPool,
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            IValidatorManager validatorManager,
            IStateManager stateManager,
            IVirtualMachine virtualMachine,
            ICrypto crypto,
            KeyPair keyPair)
        {
            _blockManager = blockManager;
            _transactionBuilder = transactionBuilder;
            _transactionPool = transactionPool;
            _transactionManager = transactionManager;
            _validatorManager = validatorManager;
            _stateManager = stateManager;
            _crypto = crypto;
            _keyPair = keyPair;
            _virtualMachine = virtualMachine;
        }

        private static bool IsValidHexString(IEnumerable<char> hexString)
        {
            return hexString.Select(currentCharacter =>
                currentCharacter >= '0' && currentCharacter <= '9' ||
                currentCharacter >= 'a' && currentCharacter <= 'f' ||
                currentCharacter >= 'A' && currentCharacter <= 'F').All(isHexCharacter => isHexCharacter);
        }

        private static string EraseHexPrefix(string hexString)
        {
            if (hexString.StartsWith("0x"))
                hexString = hexString.Substring(2);
            return hexString;
        }

        /*
         * GetTransaction
         * blockHash, UInt256
        */
        public string GetTransaction(string[] arguments)
        {
            arguments[1] = EraseHexPrefix(arguments[1]);
            if (!IsValidHexString(arguments[1]))
            {
                return null;
            }

            var tx = _transactionManager.GetByHash(arguments[1].HexToUInt256());
            return ProtoUtils.ParsedObject(tx);
            /*
            var type = tx.Transaction.Type;
            var txType = tx.Transaction.GetType().GetField(type.ToString());
            var txByType = txType.GetValue(tx.Transaction);
            var value = (UInt256) txByType.GetType().GetField("Value").GetValue(txByType);
            var to = (UInt160) txByType.GetType().GetField("To").GetValue(txByType);
            if (type != TransactionType.Contract)
            {
                to = (UInt160) txByType.GetType().GetField("Recipient").GetValue(txByType);
            }
            if (type == Tra)
            var assetName = (Asset) txByType.GetType().GetField("Asset").GetValue(txByType);
            var 
            return
                $"Hash: {tx.Hash.ToHex()}\n" +
                $"Signature: {tx.Signature.ToByteArray()}\n" +
                $"Type: {tx.Transaction.Type}\n" +
                $"Nonce: {tx.Transaction.Nonce}\n" + value == null ? $"Value: {value.ToString()} : "";*/
        }

        /*
         * GetBlock
         * blockHash, UInt256
        */
        public string GetBlock(string[] arguments)
        {
            if (arguments.Length != 2)
                return null;
            var value = EraseHexPrefix(arguments[1]);
            return ulong.TryParse(value, out var blockHeight)
                ? ProtoUtils.ParsedObject(_blockManager.GetByHeight(blockHeight))
                : ProtoUtils.ParsedObject(_blockManager.GetByHash(arguments[1].HexToUInt256()));
        }

        public string GetBalances(string[] arguments)
        {
            if (arguments.Length != 2)
                return null;
            arguments[1] = EraseHexPrefix(arguments[1]);
            if (!IsValidHexString(arguments[1]))
                return null;
            var assetNames = _stateManager.LastApprovedSnapshot.Assets.GetAssetNames();
            var result = "Balances:";
            foreach (var assetName in assetNames)
            {
                var asset = _stateManager.LastApprovedSnapshot.Assets.GetAssetByName(assetName);
                if (asset is null)
                    continue;
                var balance =
                    _stateManager.LastApprovedSnapshot.Balances.GetAvailableBalance(
                        arguments[1].HexToUInt160(), asset.Hash);
                result += $"\n - {assetName}: {balance}";
            }

            return result;
        }

        /*
         * GetBalance
         * address, UInt160
         * asset, UInt160
        */
        public Money GetBalance(string[] arguments)
        {
            if (arguments.Length != 3)
                return null;
            arguments[1] = EraseHexPrefix(arguments[1]);
            if (!IsValidHexString(arguments[1]))
                return null;
            var asset = _stateManager.LastApprovedSnapshot.Assets.GetAssetByName(arguments[2]);
            if (asset == null)
                return null;
            return _stateManager.LastApprovedSnapshot.Balances
                .GetAvailableBalance(arguments[1].HexToUInt160(), asset.Hash);
        }
        
        public string DeployContract(string[] arguments)
        {
            var from = _crypto.ComputeAddress(_keyPair.PublicKey.Buffer.ToByteArray()).ToUInt160();
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            var hash = from.Buffer.ToByteArray().Concat(BitConverter.GetBytes(nonce)).ToHash160();
            var contractCode = arguments[1].HexToBytes();
            if (!_virtualMachine.VerifyContract(contractCode))
                return "Unable to validate smart-contract code";
            Console.WriteLine("Contract Hash: " + hash.Buffer.ToHex());
            var tx = _transactionBuilder.DeployTransaction(from, null, contractCode, ContractVersion.Wasm);
            var signedTx = _transactionManager.Sign(tx, _keyPair);
            _transactionPool.Add(signedTx);
            return signedTx.Hash.Buffer.ToHex();
        }
        
        public string CallContract(string[] arguments)
        {
            var from = _crypto.ComputeAddress(_keyPair.PublicKey.Buffer.ToByteArray()).ToUInt160();
            var contractHash = arguments[1].HexToUInt160();
            var contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(contractHash);
            if (contract is null)
                return $"Unable to find contract by hash {contractHash.Buffer.ToHex()}";
            Console.WriteLine("Code: " + contract.Wasm.ToByteArray().ToHex());
            var status = _stateManager.SafeContext(() =>
            {
                _stateManager.NewSnapshot();
                var result = _virtualMachine.InvokeContract(contract, from, new byte[]{});
                _stateManager.Rollback();
                return result;
            });
            return status == ExecutionStatus.Ok ? "Contract has been successfully executed" : "Contract execution failed";
        }

        public string InvokeContract(string[] arguments)
        {
            var contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(arguments[1].HexToUInt160());
            
            throw new NotImplementedException();
        }

        public string Help(string[] arguments)
        {
            var methods = GetType().GetMethods().Select(m =>
            {
                var args = m.GetParameters().Select(arg => $"{arg.ParameterType.Name} {arg.Name}");
                return $" - {m.Name.ToLower()}: {string.Join(", ", args)}";
            });
            return "Commands:\n" + string.Join("\n", methods);
        }

        /*
         * GetTransactionPool
        */
        public IEnumerable<string> GetTransactionPool(string[] arguments)
        {
            return _transactionPool.Transactions.Values.Select(transaction => transaction.Hash.ToHex()).ToList();
        }

        /*
         * SignBlock
         * blockHash, UInt256
        */
        public string SignBlock(string[] arguments)
        {
            if (arguments.Length != 2 || arguments[1].Length != TxLength)
                return null;

            arguments[1] = EraseHexPrefix(arguments[1]);
            if (!IsValidHexString(arguments[1]))
                return null;
            var block = _blockManager.GetByHash(arguments[1].HexToUInt256());
            return _blockManager.Sign(block.Header, _keyPair).ToByteArray().ToString();
        }

        public string SignTransaction(string[] arguments)
        {
            var to = arguments[1].HexToUInt160();
            var asset = _stateManager.LastApprovedSnapshot.Assets.GetAssetByName(arguments[2]);
            var value = Money.Parse(arguments[3]);
            var from = _crypto.ComputeAddress(_keyPair.PublicKey.Buffer.ToByteArray()).ToUInt160();
            var tx = _transactionBuilder.ContractTransaction(from, to, asset, value);
            if (arguments.Length >= 4)
                tx.Fee = Money.Parse(arguments[4]).ToUInt256();
            var signedTx = _transactionManager.Sign(tx, _keyPair);
            return signedTx.Signature.ToString();
        }

        public string SendRawTransaction(string[] arguments)
        {
            if (arguments.Length != 3)
                return null;
            var rawTx = arguments[1].HexToBytes();
            var tx = Transaction.Parser.ParseFrom(rawTx);
            var sig = arguments[2].HexToBytes().ToSignature();
            var result = _transactionPool.Add(tx, sig);
            Console.WriteLine($"Status: {result}");
            Console.WriteLine($"Hash: {tx.ToHash256().ToHex()}");
            return "";
        }

        /*
         * SendTransaction:
         * 1. to, UInt160,
         * 2. assetName, string
         * 3. value, UInt256,
         * 4. fee, UInt256
        */
        public string SendTransaction(string[] arguments)
        {
            var to = arguments[1].HexToUInt160();
            var asset = _stateManager.LastApprovedSnapshot.Assets.GetAssetByName(arguments[2]);
            var value = Money.Parse(arguments[3]);
            var fee = Money.Parse(arguments[4]);
            var from = _crypto.ComputeAddress(_keyPair.PublicKey.Buffer.ToByteArray()).ToUInt160();
            var tx = _transactionBuilder.ContractTransaction(from, to, asset, value);
            var signedTx = _transactionManager.Sign(tx, _keyPair);
            _transactionPool.Add(signedTx);
            return signedTx.Hash.ToHex();
        }

        /// <summary>
        /// Verify Transaction:
        ///  1. raw transaction in hex
        ///  2. raw signature in hex
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public string VerifyTransaction(string[] arguments)
        {
            arguments[1] = "0x080322160a14e855e8f8e5f66a84c62800e9fc8fa06d77c35baf323b0a160a14309ef5b9fed49a18eb3ea1d090c79df690936b9812160a146bc32575acb8754886dc283c2c8ac54b1bd931951a090a072386f26fc100007200";
            arguments[2] = "0x01aa279be6f82767f7d1c75a966b33c13d2ae573f7f39ccf7557d86cc0cdb8aa5731b2639ff6ef7555232fd1ed6e27e281e5ae96de22b49083df380fb892485761";
            
            var tx = Transaction.Parser.ParseFrom(
                arguments[1].HexToBytes());
            var sig = arguments[2].HexToBytes().ToSignature();
            Console.WriteLine($"Tx Hash: {tx.ToHash256().Buffer.ToHex()}");
            var accepted = new AcceptedTransaction
            {
                Transaction = tx,
                Hash = tx.ToHash256(),
                Signature = sig
            };
            Console.WriteLine("Transaction validity: " + _transactionManager.Verify(accepted));
            Console.WriteLine(_transactionManager.VerifySignature(accepted) == OperatingError.Ok
                ? "Signature validity: OK"
                : "Signature validity: INVALID");
            Console.WriteLine(_transactionManager.VerifySignature(accepted, false) == OperatingError.Ok
                ? "Signature validity: OK"
                : "Signature validity: INVALID");
            return "\n";
        }
    }
}