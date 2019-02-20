using System;
using System.Linq;
using Google.Protobuf.WellKnownTypes;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Config;
using Phorkus.Proto;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.Genesis
{
    public class GenesisBuilder : IGenesisBuilder
    {
        public const ulong GenesisConsensusData = 2083236893UL;

        private readonly IConfigManager _configManager;
        private readonly ICrypto _crypto;
        private readonly ITransactionManager _transactionManager;

        public GenesisBuilder(
            IConfigManager configManager,
            ICrypto crypto,
            ITransactionManager transactionManager)
        {
            _crypto = crypto;
            _transactionManager = transactionManager;
            _configManager = configManager;
        }

        private BlockWithTransactions _genesisBlock;
        
        public BlockWithTransactions Build()
        {
            if (_genesisBlock != null)
                return _genesisBlock;

            var genesisConfig = _configManager.GetConfig<GenesisConfig>("genesis");
            if (genesisConfig?.PrivateKey is null)
                throw new ArgumentNullException(nameof(genesisConfig.PrivateKey), "You must specify private key in genesis config section");
            var keyPair = new KeyPair(genesisConfig.PrivateKey.HexToBytes().ToPrivateKey(), _crypto);
            var address = _crypto.ComputeAddress(keyPair.PublicKey.Buffer.ToByteArray()).ToUInt160();
            
            var genesisTimestamp = new DateTime(kind: DateTimeKind.Utc,
                year: 2019, month: 1, day: 1, hour: 00, minute: 00, second: 00).ToTimestamp();
            
            var txsBefore = new Transaction[]{};
            var genesisTransactions = txsBefore.ToArray();
            
            var nonce = 0ul;
            foreach (var tx in genesisTransactions)
            {
                tx.From = address;
                tx.Nonce = nonce++;
            }
            
            var signed = genesisTransactions.Select(tx => _transactionManager.Sign(tx, keyPair));
            var acceptedTransactions = signed as TransactionReceipt[] ?? signed.ToArray();
            var txHashes = acceptedTransactions.Select(tx => tx.Hash).ToArray();
            
            var header = new BlockHeader
            {
                PrevBlockHash = UInt256Utils.Zero,
                MerkleRoot = MerkleTree.ComputeRoot(txHashes) ?? UInt256Utils.Zero,
                Timestamp = (ulong) genesisTimestamp.Seconds,
                Index = 0,
                Validator = keyPair.PublicKey,
                StateHash = UInt256Utils.Zero,
                Nonce = GenesisConsensusData
            };
            
            var result = new Block
            {
                Hash = header.ToHash256(),
                TransactionHashes = { txHashes },
                Header = header
            };
            
            _genesisBlock = new BlockWithTransactions(result, acceptedTransactions.ToArray());
            return _genesisBlock;
        }
    }
}