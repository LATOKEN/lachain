using System.Collections.Generic;
using System.Linq;
using NeoSharp.Core.Cryptography;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.OperationManager;
using NeoSharp.Core.Models.Transactions;
using NeoSharp.Core.Network;
using NeoSharp.Core.SmartContract;
using NeoSharp.Types;
using NeoSharp.Types.ExtensionMethods;

namespace NeoSharp.Core.Blockchain.Genesis
{
    public class GenesisAssetsBuilder : IGenesisAssetsBuilder
    {
        private readonly ISigner<Transaction> _transactionSigner;
        private readonly NetworkConfig _networkConfig;

        public GenesisAssetsBuilder(ISigner<Transaction> transactionSigner, NetworkConfig networkConfig)
        {
            _transactionSigner = transactionSigner;
            _networkConfig = networkConfig;

            /*var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, true);
            var configuration = builder.Build();
            _networkConfig = new NetworkConfig(configuration);*/
        }

        /// <inheritdoc />
        public RegisterTransaction BuildGoverningTokenRegisterTransaction()
        {
            var tx = new RegisterTransaction
            {
                Flags = TransactionFlags.None,
                From = UInt160.Zero,
                Nonce = 0,
                AssetType = AssetType.GoverningToken,
                Name = "LA",
                Supply = UInt256.FromDec("100000000000000000000000000"),
                Precision = 18,
                Owner = UInt160.Zero
            };
            _transactionSigner.Sign(tx);
            return tx;
        }

        public MinerTransaction BuildGenesisMinerTransaction()
        {
            var tx = new MinerTransaction
            {
                Flags = TransactionFlags.None,
                From = UInt160.Zero,
                Nonce = 2083236893
            };
            _transactionSigner.Sign(tx);
            return tx;
        }

        public IssueTransaction BuildGenesisTokenIssue(PublicKey owner, UInt256 value, UInt160 asset)
        {
            var tx = new IssueTransaction
            {
                From = UInt160.Zero,
                Flags = TransactionFlags.None,
                Nonce = 0,
                Amount = value,
                Asset = asset
            };
            _transactionSigner.Sign(tx);
            return tx;
        }

        public IEnumerable<IssueTransaction> IssueTransactionsToOwners(UInt256 value, params UInt160[] assets)
        {
            var txs = new List<IssueTransaction>();
            foreach (var validator in _networkConfig.StandByValidator)
            foreach (var asset in assets)
                txs.Add(BuildGenesisTokenIssue(new PublicKey(validator.HexToBytes()), value, asset));
            return txs;
        }

        /// <inheritdoc />
        public UInt160 BuildGenesisNextConsensusAddress()
        {
            var genesisValidators = GenesisStandByValidators();
            return ContractFactory
                .CreateMultiplePublicKeyRedeemContract(genesisValidators.Length - (genesisValidators.Length - 1) / 3,
                    genesisValidators).Code.ScriptHash;
        }

        private PublicKey[] GenesisStandByValidators()
        {
            return _networkConfig.StandByValidator.Select(u => new PublicKey(u.HexToBytes())).ToArray();
        }
    }
}