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

        public OperatingError Persist(Transaction transaction, UInt256 hash)
        {
            var result = Verify(transaction);
            if (result != OperatingError.Ok)
                return result;
            var registerTx = transaction.Register;
            var asset = new Asset
            {
                Hash = registerTx.ToHash160(),
                Version = 0,
                Type = registerTx.Type,
                Name = registerTx.Name,
                Supply = registerTx.Supply,
                Decimals = registerTx.Decimals,
                Owner = registerTx.Owner
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