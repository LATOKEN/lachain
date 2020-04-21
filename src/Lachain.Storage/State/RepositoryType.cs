namespace Lachain.Storage.State
{
    public enum RepositoryType : uint
    {
        MetaRepository = 0,
        BalanceRepository = 1,
        ContractRepository = 4,
        StorageRepository = 5,
        TransactionRepository = 6,
        BlockRepository = 7,
        EventRepository = 9,
        ValidatorRepository = 10,
    }
}