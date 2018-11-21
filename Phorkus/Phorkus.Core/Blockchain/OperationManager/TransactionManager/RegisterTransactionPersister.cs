using Phorkus.Core.Proto;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class RegisterTransactionPersister : ITransactionPersister
    {
        private readonly IAssetRepository _assetRepository;

        public RegisterTransactionPersister(IAssetRepository assetRepository)
        {
            _assetRepository = assetRepository;
        }

        public OperatingError Execute(Transaction transaction)
        {
            var result = Verify(transaction);
            if (result != OperatingError.Ok)
                return result;
            var registerTx = transaction.Register;
            var minter = registerTx.Minter;
            if (minter is null)
                minter = registerTx.Owner;
            var asset = new Asset
            {
                Hash = transaction.Register.ToHash160(),
                Type = registerTx.Type,
                Name = registerTx.Name,
                Supply = registerTx.Supply,
                Decimals = registerTx.Decimals,
                Owner = registerTx.Owner,
                Minter = minter
            };
            return !_assetRepository.AddAsset(asset) ? OperatingError.AlreadyExists : OperatingError.Ok;
        }
        
        public OperatingError Verify(Transaction transaction)
        {
            if (transaction.Type != TransactionType.Register)
                return OperatingError.InvalidTransaction;
            if (transaction.Register is null)
                return OperatingError.InvalidTransaction;
            return OperatingError.Ok;
        }
    }
}