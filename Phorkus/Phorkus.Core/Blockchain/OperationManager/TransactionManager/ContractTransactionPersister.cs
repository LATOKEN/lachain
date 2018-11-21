using Phorkus.Core.Proto;
using Phorkus.Core.Storage;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class ContractTransactionPersister : ITransactionPersister
    {
        private readonly IBalanceRepository _balanceRepository;

        public ContractTransactionPersister(IBalanceRepository balanceRepository)
        {
            _balanceRepository = balanceRepository;
        }

        public OperatingError Confirm(Transaction transaction, UInt256 hash)
        {
            throw new System.NotImplementedException();
        }

        public OperatingError Verify(Transaction transaction)
        {
            throw new System.NotImplementedException();
        }
    }
}