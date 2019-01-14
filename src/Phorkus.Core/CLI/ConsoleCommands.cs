using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Storage.Repositories;
using Phorkus.Storage.State;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.CLI
{
    public class ConsoleCommands : IConsoleCommands
    {
        private const uint AddressLength = 20;
        private const uint TxLength = 32;

        private readonly IGlobalRepository _globalRepository;
        private readonly IValidatorManager _validatorManager;
        private readonly ITransactionPool _transactionPool;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly ICrypto _crypto;
        private readonly IStateManager _stateManager;
        private readonly KeyPair _keyPair;

        public ConsoleCommands(
            IGlobalRepository globalRepository,
            ITransactionBuilder transactionBuilder,
            ITransactionPool transactionPool,
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            IValidatorManager validatorManager,
            IStateManager stateManager,
            ICrypto crypto,
            KeyPair keyPair)
        {
            _blockManager = blockManager;
            _globalRepository = globalRepository;
            _transactionBuilder = transactionBuilder;
            _transactionPool = transactionPool;
            _transactionManager = transactionManager;
            _validatorManager = validatorManager;
            _stateManager = stateManager;
            _crypto = crypto;
            _keyPair = keyPair;
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
            var from = arguments[1].HexToUInt160();
            var to = arguments[2].HexToUInt160();
            var asset = _stateManager.LastApprovedSnapshot.Assets.GetAssetByName(arguments[3]);
            var value = Money.Parse(arguments[4]);
            var fee = Money.Parse(arguments[5]);
            var tx = _transactionBuilder.ContractTransaction(from, to, asset, value, fee, null);
            var signedTx = _transactionManager.Sign(tx, _keyPair);
            return signedTx.Signature.ToString();
        }

        public string SendRawTransaction(string[] arguments)
        {
            if (arguments.Length != 2)
                return null;
            var rawTx = arguments[1].HexToBytes();
            var tx = SignedTransaction.Parser.ParseFrom(rawTx);
            _transactionPool.Add(tx);
            return tx.Hash.ToHex();
        }

        /*
         * SendTransaction
         * from, UInt160,
         * to, UInt160,
         * assetName, string
         * value, UInt256,
         * fee, UInt256
        */
        public string SendTransaction(string[] arguments)
        {
            var from = arguments[1].HexToUInt160();
            var to = arguments[2].HexToUInt160();
            var asset = _stateManager.LastApprovedSnapshot.Assets.GetAssetByName(arguments[3]);
            var value = Money.Parse(arguments[4]);
            var fee = Money.Parse(arguments[5]);
            var tx = _transactionBuilder.ContractTransaction(from, to, asset, value, fee, null);
            var signedTx = _transactionManager.Sign(tx, _keyPair);
            _transactionPool.Add(signedTx);
            return signedTx.Hash.ToHex();
        }
    }
}