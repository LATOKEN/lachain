using Phorkus.Core.Blockchain.State;
using Phorkus.Proto;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class RegisterTransactionExecuter : ITransactionExecuter
    {
        private readonly IMultisigVerifier _multisigVerifier;
        private readonly IAssetRepository _assetRepository;

        public RegisterTransactionExecuter(
            IMultisigVerifier multisigVerifier,
            IAssetRepository assetRepository)
        {
            _assetRepository = assetRepository;
            _multisigVerifier = multisigVerifier;
        }
        
        public OperatingError Execute(Block block, Transaction transaction, IBlockchainSnapshot snapshot)
        {
            /* don't execute invalid transactions */
            var result = Verify(transaction);
            if (result != OperatingError.Ok)
                return result;
            var registerTx = transaction.Register;
            /* don't allow to register governing tokens in non-genesis block */
            if (registerTx.Type == AssetType.Governing && block.Header.Index != 0)
                return OperatingError.InvalidTransaction;
            /* check asset registration */
            /* TODO: "only validators can register new tokens" */
            /* check transaction's minter or set owner */
            var minter = registerTx.Minter;
            if (minter is null)
                minter = registerTx.Owner;
            /* create new asset and register it */
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
            /* transaction type should be register */
            if (transaction.Type != TransactionType.Register)
                return OperatingError.InvalidTransaction;
            /* transaction structure should be defined */
            if (transaction.Register is null)
                return OperatingError.InvalidTransaction;
            return OperatingError.Ok;
        }
    }
}