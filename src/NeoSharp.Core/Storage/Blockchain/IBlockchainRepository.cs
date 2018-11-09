namespace NeoSharp.Core.Storage.Blockchain
{
    public interface IBlockchainRepository :
        IGlobalRepository,
        IBlockRepository,
        ITransactionRepository,
        IAssetRepository,
        IContractRepository,
        IStorageRepository
    {
    }
}