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
        
        public Transaction TransferTransaction(UInt160 from, UInt160 to, UInt160 asset, Money value)
        {
            var nonce = _transactionRepository.GetTotalTransactionCount(from);
            var contractTx = new ContractTransaction
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
                Contract = contractTx,
                Signature = SignatureUtils.Zero
            };
            return tx;
        }
        
        public Transaction MinerTransaction(UInt160 from)
        {
            var nonce = _transactionRepository.GetTotalTransactionCount(@from);
            var miner = new MinerTransaction
            {
                Miner = from
            };
            var tx = new Transaction
            {
                Type = TransactionType.Contract,
                Version = 0,
                Flags = 0,
                From = from,
                Nonce = nonce,
                Miner = miner,
                Signature = SignatureUtils.Zero
            };
            return tx;
        }
    }
}