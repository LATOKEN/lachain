using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Proto;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Blockchain
{
    public class TransactionFactory : ITransactionFactory
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransactionManager _transactionManager;

        public TransactionFactory(ITransactionRepository transactionRepository,
            ITransactionManager transactionManager)
        {
            _transactionRepository = transactionRepository;
            _transactionManager = transactionManager;
        }
        
        public Transaction TransferMoney(UInt160 from, UInt160 to, UInt160 asset, Money value, uint nonceOffset = 0)
        {
            var nonce = _transactionRepository.GetTotalTransactionCount(from) + nonceOffset;
            var contract = new ContractTransaction
            {
                Asset = asset,
                To = to,
                Value = value.ToUInt256()
            };
            var tx = new Transaction
            {
                Type = TransactionType.Contract,
                Version = 0,
                Flags = 0,
                From = from,
                Nonce = nonce,
                Contract = contract,
                Signature = SignatureUtils.Zero
            };
            return tx;
        }
    }
}