using System;
using System.Linq;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Config;
using Lachain.Proto;
using Lachain.Crypto;
using Lachain.Crypto.Misc;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.Genesis
{
    public class GenesisBuilder : IGenesisBuilder
    {
        public const ulong GenesisConsensusData = 2083236893UL;

        private readonly IConfigManager _configManager;

        public GenesisBuilder(IConfigManager configManager)
        {
            _configManager = configManager;
        }

        private BlockWithTransactions? _genesisBlock;

        public BlockWithTransactions Build()
        {
            if (_genesisBlock != null)
                return _genesisBlock;

            var genesisConfig = _configManager.GetConfig<GenesisConfig>("genesis") ??
                                throw new InvalidOperationException("No genesis config found");

            var fromAddress = UInt160Utils.Zero; // mint initial tokens from zero address
            var balances = genesisConfig!.Balances
                .OrderBy(x => x.Key)
                .ToArray();
            var genesisTransactions = balances.Select((t, i) => new Transaction
                {
                    From = fromAddress,
                    Nonce = (ulong) i,
                    Value = Money.Parse(t.Value).ToUInt256(),
                    To = t.Key.HexToUInt160(),
                    GasPrice = 0,
                })
                .Select(tx => new TransactionReceipt
                {
                    Transaction = tx,
                    Hash = tx.FullHash(SignatureUtils.Zero, false),
                    Signature = SignatureUtils.Zero,
                })
                .ToList();

            var txHashes = genesisTransactions.Select(tx => tx.Hash).ToArray();

            var header = new BlockHeader
            {
                PrevBlockHash = UInt256Utils.Zero,
                MerkleRoot = MerkleTree.ComputeRoot(txHashes) ?? UInt256Utils.Zero,
                Index = 0,
                StateHash = UInt256Utils.Zero,
                Nonce = GenesisConsensusData
            };

            var result = new Block
            {
                Hash = header.Keccak(),
                TransactionHashes = {txHashes},
                Header = header
            };

            _genesisBlock = new BlockWithTransactions(result, genesisTransactions.ToArray());
            return _genesisBlock;
        }
    }
}