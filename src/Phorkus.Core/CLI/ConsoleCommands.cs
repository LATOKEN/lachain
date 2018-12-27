using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Storage.RocksDB.Repositories;
using Phorkus.Storage.State;
using Phorkus.Utility;
using Phorkus.Utility.Utils;
using HexUtils = Phorkus.Core.Utils.HexUtils;

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
        private readonly IBlockchainStateManager _blockchainStateManager;
        private readonly KeyPair _keyPair;

        public ConsoleCommands(
            IGlobalRepository globalRepository,
            ITransactionBuilder transactionBuilder,
            ITransactionPool transactionPool,
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            IValidatorManager validatorManager,
            IBlockchainStateManager blockchainStateManager,
            ICrypto crypto,
            KeyPair keyPair)
        {
            _blockManager = blockManager;
            _globalRepository = globalRepository;
            _transactionBuilder = transactionBuilder;
            _transactionPool = transactionPool;
            _transactionManager = transactionManager;
            _validatorManager = validatorManager;
            _blockchainStateManager = blockchainStateManager;
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
        public SignedTransaction GetTransaction(string[] arguments)
        {
            arguments[1] = EraseHexPrefix(arguments[1]);
            return !IsValidHexString(arguments[1])
                ? null
                : _transactionManager.GetByHash(arguments[1].HexToUInt256());
        }

        /*
         * GetBlock
         * blockHash, UInt256
        */
        public Block GetBlock(string[] arguments)
        {
            if (arguments.Length != 2)
                return null;
            var value = EraseHexPrefix(arguments[1]);
            if (ulong.TryParse(value, out var blockHeight))
                return _blockManager.GetByHeight(blockHeight);
            return _blockManager.GetByHash(arguments[1].HexToUInt256());
        }

        public string GetBalances(string[] arguments)
        {
            if (arguments.Length != 2)
                return null;
            arguments[1] = EraseHexPrefix(arguments[1]);
            if (!IsValidHexString(arguments[1]))
                return null;
            var assetNames = _blockchainStateManager.LastApprovedSnapshot.Assets.GetAssetNames();
            var result = "Balances:";
            foreach (var assetName in assetNames)
            {
                var asset = _blockchainStateManager.LastApprovedSnapshot.Assets.GetAssetByName(assetName);
                if (asset is null)
                    continue;
                var balance =
                    _blockchainStateManager.LastApprovedSnapshot.Balances.GetAvailableBalance(
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
            var asset = _blockchainStateManager.LastApprovedSnapshot.Assets.GetAssetByName(arguments[2]);
            if (asset == null)
                return null;
            return _blockchainStateManager.LastApprovedSnapshot.Balances
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
        public IEnumerable<SignedTransaction> GetTransactionPool(string[] arguments)
        {
            return _transactionPool.Transactions.Values;
        }

        /*
         * SignBlock
         * blockHash, UInt256
        */
        public Signature SignBlock(string[] arguments)
        {
            if (arguments.Length != 2 || arguments[1].Length != TxLength)
                return null;

            arguments[1] = EraseHexPrefix(arguments[1]);
            if (!IsValidHexString(arguments[1]))
                return null;
            var block = _blockManager.GetByHash(arguments[1].HexToUInt256());
            return _blockManager.Sign(block.Header, _keyPair);
        }
        
        /*
         * SendTransaction
         * from, UInt160,
         * to, UInt160,
         * assetName, string
         * value, UInt256,
         * fee, UInt256
        */
        public SignedTransaction SendTransaction(string[] arguments)
        {
            var from = arguments[1].HexToUInt160();
            var to = arguments[2].HexToUInt160();
            var asset = _blockchainStateManager.LastApprovedSnapshot.Assets.GetAssetByName(arguments[3]);
            var value = arguments[4].HexToUInt256();
            var fee = arguments[5].HexToUInt256();
            var tx = _transactionBuilder.ContractTransaction(from, to, asset, value, fee, null);
            var signedTx = _transactionManager.Sign(tx, _keyPair);
            _transactionPool.Add(signedTx);
            return signedTx;
        }

     /*
         public Signature SignTransaction(string[] arguments)
         {
             if (arguments.Length != 2 || arguments[1].Length != TxLength)
             {
                 return null;
             }
    
             arguments[1] = EraseHexPrefix(arguments[1]);
             if (!IsValidHexString(arguments[1]))
             {
                 return null;
             }

             Block block = _transactionPool.GetByHash(HexUtils.HexToUInt256(arguments[1]));
             return _blockManager.Sign(block.Header, _keyPair);
         }*/
    }
}