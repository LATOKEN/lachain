using System;
using System.Collections.Generic;
using Google.Protobuf;
using Phorkus.Core.Proto;
using Phorkus.Core.Uilts;

namespace Phorkus.Core.Blockchain.Genesis
{
    public class GenesisAssetsBuilder : IGenesisAssetsBuilder
    {
        private readonly IEnumerable<string> _validators;
        
        public GenesisAssetsBuilder(IEnumerable<string> validators)
        {
            _validators = validators ?? throw new ArgumentException(nameof(validators));

            /*var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, true);
            var configuration = builder.Build();
            _networkConfig = new NetworkConfig(configuration);*/
        }

        /// <inheritdoc />
        public Transaction BuildGoverningTokenRegisterTransaction()
        {
            var tx = new Transaction
            {
                Hash = UInt256Utils.Zero,
                Type = TransactionType.Register,
                Version = 0,
                Flags = (ulong) TransactionFlag.None,
                From = UInt160Utils.Zero,
                Register = new RegisterTransaction
                {
                    Type = AssetType.Governing,
                    Name = "LA",
                    Supply = Fixed256Utils.FromDecimal(100_000_000),
                    Decimals = 18,
                    Owner = UInt160Utils.Zero
                },
                Nonce = 0
            };
            tx.Hash = new UInt256
            {
                Buffer = ByteString.CopyFrom(tx.ToHash256().ToByteArray())
            };
            return tx;
        }

        public Transaction BuildGenesisMinerTransaction()
        {
            var tx = new Transaction
            {
                Hash = UInt256Utils.Zero,
                Type = TransactionType.Register,
                Version = 0,
                Flags = (ulong) TransactionFlag.None,
                From = UInt160Utils.Zero,
                Miner = new MinerTransaction
                {
                    Miner = UInt160Utils.Zero
                },
                Nonce = 0
            };
            tx.Hash = new UInt256
            {
                Buffer = ByteString.CopyFrom(tx.ToHash256().ToByteArray())
            };
            return tx;
        }

        public Transaction BuildGenesisTokenIssue(PublicKey owner, Fixed256 supply, UInt160 asset)
        {
            var tx = new Transaction
            {
                Hash = UInt256Utils.Zero,
                Type = TransactionType.Register,
                Version = 0,
                Flags = (ulong) TransactionFlag.None,
                From = UInt160Utils.Zero,
                Issue = new IssueTransaction
                {
                    Asset = asset,
                    Supply = supply
                },
                Nonce = 0
            };
            tx.Hash = new UInt256
            {
                Buffer = ByteString.CopyFrom(tx.ToHash256().ToByteArray())
            };
            return tx;
        }

        public IEnumerable<Transaction> IssueTransactionsToOwners(Fixed256 value, params UInt160[] assets)
        {
            var txs = new List<Transaction>();
            foreach (var validator in _validators)
            foreach (var asset in assets)
            {
                var publicKey = new PublicKey
                {
                    Buffer = ByteString.CopyFrom(validator.HexToBytes())
                };
                txs.Add(BuildGenesisTokenIssue(publicKey, value, asset));
            }
            return txs;
        }
    }
}