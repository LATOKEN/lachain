using System;
using System.Linq;
using Google.Protobuf.WellKnownTypes;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Proto;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.Genesis
{
    public class GenesisBuilder : IGenesisBuilder
    {
        public const ulong GenesisConsensusData = 2083236893UL;

        private readonly ICrypto _crypto;
        private readonly ITransactionManager _transactionManager;
        private readonly KeyPair _keyPair;

        public GenesisBuilder(
            ICrypto crypto,
            ITransactionManager transactionManager)
        {
            _crypto = crypto;
            _transactionManager = transactionManager;
            /* TODO: "replace this private key with encrypted private key with threshold paillier cryptosystem" */
            _keyPair = new KeyPair("8a04748ce6329cf899cee3f3e0f4720d1a6d917a9183a11b323315de2ffbf84d".HexToBytes().ToPrivateKey(), crypto);
        }

        private BlockWithTransactions _genesisBlock;
        
        public BlockWithTransactions Build()
        {
            if (_genesisBlock != null)
                return _genesisBlock;

            var address = _crypto.ComputeAddress(_keyPair.PublicKey.Buffer.ToByteArray()).ToUInt160();
            
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
            
            var signed = genesisTransactions.Select(tx => _transactionManager.Sign(tx, _keyPair));
            var acceptedTransactions = signed as AcceptedTransaction[] ?? signed.ToArray();
            var txHashes = acceptedTransactions.Select(tx => tx.Hash).ToArray();
            
            var header = new BlockHeader
            {
                Version = 0,
                PrevBlockHash = UInt256Utils.Zero,
                MerkleRoot = MerkleTree.ComputeRoot(txHashes) ?? UInt256Utils.Zero,
                Timestamp = (ulong) genesisTimestamp.Seconds,
                Index = 0,
                Validator = _keyPair.PublicKey,
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