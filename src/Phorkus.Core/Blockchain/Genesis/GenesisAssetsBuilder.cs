using System;
using System.Collections.Generic;
using Org.BouncyCastle.Asn1;
using Phorkus.Core.Consensus;
using Phorkus.Core.Config;
using Phorkus.Proto;
using Phorkus.Utility;
using Phorkus.Utility.Utils;
using Phorkus.Crypto;

namespace Phorkus.Core.Blockchain.Genesis
{
    public class GenesisAssetsBuilder : IGenesisAssetsBuilder
    {
        private readonly ICrypto _crypto;
        private readonly IEnumerable<string> _validators;

        public GenesisAssetsBuilder(IConfigManager configManager, ICrypto crypto)
        {
            _crypto = crypto;
            var config = configManager.GetConfig<ConsensusConfig>("consensus");
            if (config is null)
                throw new ArgumentNullException(nameof(configManager), "Unable to resolve consensus configuration");
            _validators = config.ValidatorsKeys;
        }

        public Transaction BuildGoverningTokenRegisterTransaction(UInt160 owner)
        {
            var tx = new Transaction
            {
                Type = TransactionType.Register,
                Version = 0,
                Flags = (ulong) TransactionFlag.None,
                From = UInt160Utils.Zero,
                Register = new RegisterTransaction
                {
                    Type = AssetType.Governing,
                    Name = "LA",
                    Supply = Money.FromDecimal(100_000_000m).ToUInt256(),
                    Decimals = 18,
                    Owner = owner
                },
                Nonce = 0
            };
            return tx;
        }


        public Transaction BuildPlatformTokenRegisterTransaction(UInt160 owner, string platformToken, uint supply,
            uint precision)
        {
            var tx = new Transaction
            {
                Type = TransactionType.Register,
                Version = 0,
                Flags = (ulong) TransactionFlag.None,
                From = UInt160Utils.Zero,
                Register = new RegisterTransaction
                {
                    Type = AssetType.Platform,
                    Name = platformToken,
                    Supply = Money.FromDecimal(supply).ToUInt256(),
                    Decimals = precision,
                    Owner = owner
                },
                Nonce = 0
            };
            return tx;
        }


        public Transaction BuildGenesisMinerTransaction()
        {
            var tx = new Transaction
            {
                Type = TransactionType.Miner,
                Version = 0,
                Flags = (ulong) TransactionFlag.None,
                From = UInt160Utils.Zero,
                Miner = new MinerTransaction
                {
                    Miner = UInt160Utils.Zero
                },
                Nonce = 0
            };
            return tx;
        }

        public Transaction BuildGenesisTokenIssue(PublicKey owner, Money supply, UInt160 asset)
        {
            var tx = new Transaction
            {
                Type = TransactionType.Issue,
                Version = 0,
                Flags = (ulong) TransactionFlag.None,
                From = UInt160Utils.Zero,
                Issue = new IssueTransaction
                {
                    Asset = asset,
                    Supply = supply.ToUInt256(),
                    To = _crypto.ComputeAddress(owner.Buffer.ToByteArray()).ToUInt160()
                },
                Nonce = 0
            };
            return tx;
        }

        public IEnumerable<Transaction> IssueTransactionsToOwners(Money value, params UInt160[] assets)
        {
            var txs = new List<Transaction>();
            foreach (var validator in _validators)
            foreach (var asset in assets)
            {
                var publicKey = validator.HexToBytes().ToPublicKey();
                txs.Add(BuildGenesisTokenIssue(publicKey, value, asset));
            }

            return txs;
        }
    }
}