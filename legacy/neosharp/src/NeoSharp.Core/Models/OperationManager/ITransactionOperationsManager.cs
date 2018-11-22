namespace NeoSharp.Core.Models.OperationManager
{
    public interface ITransactionOperationsManager : ISigner<Transaction>, IVerifier<Transaction>
    {
    }
}
