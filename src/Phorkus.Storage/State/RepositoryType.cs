namespace Phorkus.Storage.State
{
    public enum RepositoryType : uint
    {
        MetaRepository = 0,
        BalanceRepository = 1,
        AssetRepository = 2,
        ContractRepository = 4,
        StorageRepository = 5,
        TransactionRepository = 6,
        BlockRepository = 7,
        WithdrawalRepository = 8,
    }
}