using System.Collections.Generic;
using System.Linq;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.Pool;
using Phorkus.Core.Blockchain.State;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.CLI
{
    public class CLICommands : ICLICommands
    {
        private readonly IGlobalRepository _globalRepository;
        private readonly IValidatorManager _validatorManager;
        private readonly ITransactionPool _transactionPool;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly IBlockchainStateManager _blockchainStateManager;
        private readonly ICrypto _crypto;
        private const uint TxLength = 32;
        private const uint AddressLength = 20;
        
        
        private ThresholdKey _thresholdKey;
        private KeyPair _keyPair;

        public CLICommands(
            IGlobalRepository globalRepository,
            ITransactionBuilder transactionBuilder,
            ITransactionPool transactionPool,
            ITransactionManager transactionManager,
            ICrypto crypto,
            IBlockManager blockManager,
            IValidatorManager validatorManager,
            IBlockchainStateManager blockchainStateManager,
            ThresholdKey thresholdKey,
            KeyPair keyPair)
        {
            _blockManager = blockManager;
            _globalRepository = globalRepository;
            _transactionBuilder = transactionBuilder;
            _transactionPool = transactionPool;
            _transactionManager = transactionManager;
            _crypto = crypto;
            _validatorManager = validatorManager;
            _blockchainStateManager = blockchainStateManager;
            _keyPair = keyPair;
            _thresholdKey = thresholdKey;
        }

        private static bool IsValidHexString(IEnumerable<char> hexString)
        {
            return hexString.Select(currentCharacter =>
                (currentCharacter >= '0' && currentCharacter <= '9') ||
                (currentCharacter >= 'a' && currentCharacter <= 'f') ||
                (currentCharacter >= 'A' && currentCharacter <= 'F')).All(isHexCharacter => isHexCharacter);
        }

        private static string EraseHexPrefix(string hexString)
        {
            if (hexString.Substring(0, 2) == "0x")
            {
                return hexString.Substring(2);
            }

            return hexString;
        }

        /*
         * GetTransaction
         * blockHash, UInt256
        */
        public SignedTransaction GetTransaction(string[] arguments)
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

            return _transactionManager.GetByHash(HexUtils.HexToUInt256(arguments[1]));
        }

        /*
         * GetBlock
         * blockHash, UInt256
        */
        public Block GetBlock(string[] arguments)
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

            return _blockManager.GetByHash(HexUtils.HexToUInt256(arguments[1]));
        }

        /*
         * GetBalance
         * address, UInt160
         * asset, UInt160
        */
        public UInt256 GetBalance(string[] arguments)
        {
            if (arguments.Length != 3 || arguments[1].Length != AddressLength)
            {
                return null;
            }

            arguments[1] = EraseHexPrefix(arguments[1]);
            if (!IsValidHexString(arguments[1]))
            {
                return null;
            }

            var asset = _blockchainStateManager.LastApprovedSnapshot.Assets.GetAssetByName(arguments[2]);
            if (asset == null)
            {
                return null;
            }

            return _blockchainStateManager.LastApprovedSnapshot.Balances
                .GetBalance(HexUtils.HexToUInt160(arguments[1]), asset.Hash).ToUInt256();
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
            {
                return null;
            }

            arguments[1] = EraseHexPrefix(arguments[1]);
            if (!IsValidHexString(arguments[1]))
            {
                return null;
            }

            Block block =_blockManager.GetByHash(HexUtils.HexToUInt256(arguments[1]));
            return _blockManager.Sign(block.Header, _keyPair);
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
            Block block =_transactionPool.GetByHash(HexUtils.HexToUInt256(arguments[1]));
            return _blockManager.Sign(block.Header, _keyPair);
        }*/
    }
    
}