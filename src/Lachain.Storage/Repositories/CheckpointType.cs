namespace Lachain.Storage.Repositories
{
    public enum CheckpointType : byte
    {
        BlockHeight = 0,
        BlockHash = 1,
        BalanceStateHash = 2,
        ContractStateHash = 3,
        EventStateHash = 4,
        StorageStateHash = 5,
        TransactionStateHash = 6,
        ValidatorStateHash = 7,
    }
}