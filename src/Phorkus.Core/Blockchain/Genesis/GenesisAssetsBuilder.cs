using System.Collections.Generic;
using System.Linq;
using Phorkus.Proto;
using Phorkus.Utility;
using Phorkus.Utility.Utils;
using Phorkus.Crypto;

namespace Phorkus.Core.Blockchain.Genesis
{
    public class GenesisAssetsBuilder : IGenesisAssetsBuilder
    {
        private readonly IValidatorManager _validatorManager;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ICrypto _crypto;

        public GenesisAssetsBuilder(
            IValidatorManager validatorManager,
            ITransactionBuilder transactionBuilder,
            ICrypto crypto)
        {
            _transactionBuilder = transactionBuilder;
            _crypto = crypto;
            _validatorManager = validatorManager;            
        }

        public Transaction BuildGoverningTokenRegisterTransaction(UInt160 owner)
        {
            return _transactionBuilder.RegisterTransaction(AssetType.Governing, "LA", Money.FromDecimal(100_000_000m),
                18, owner);
        }

        public Transaction BuildPlatformTokenRegisterTransaction(UInt160 owner, string name, uint supply,
            uint precision)
        {
            return _transactionBuilder.RegisterTransaction(AssetType.Platform, name, Money.Zero, precision, owner);
        }

        public Transaction BuildGenesisMinerTransaction()
        {
            var tx = new Transaction
            {
                Type = TransactionType.Miner,
                From = UInt160Utils.Zero,
                Fee = UInt256Utils.Zero,
                Nonce = 0
            };
            return tx;
        }

        public Transaction BuildGenesisTokenIssue(PublicKey owner, Money supply, UInt160 asset)
        {
            var tx = new Transaction
            {
                Type = TransactionType.Issue,
                From = UInt160Utils.Zero,
                Issue = new IssueTransaction
                {
                    Asset = asset,
                    Supply = supply.ToUInt256(),
                    To = _crypto.ComputeAddress(owner.Buffer.ToByteArray()).ToUInt160()
                },
                Fee = UInt256Utils.Zero,
                Nonce = 0
            };
            return tx;
        }

        public IEnumerable<Transaction> IssueTransactionsToOwners(Money value, params UInt160[] assets)
        {
            var txs = new List<Transaction>();
            foreach (var validator in _validatorManager.Validators)
                txs.AddRange(assets.Select(asset => BuildGenesisTokenIssue(validator, value, asset)));
            return txs;
        }
    }
}