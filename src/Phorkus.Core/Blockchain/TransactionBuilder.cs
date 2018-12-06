using Google.Protobuf;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Proto;
using Phorkus.Core.Storage;
using Phorkus.Utility;

namespace Phorkus.Core.Blockchain
{
    public class TransactionBuilder : ITransactionBuilder
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransactionManager _transactionManager;

        public TransactionBuilder(ITransactionRepository transactionRepository,
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
                Contract = contractTx
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
                Type = TransactionType.Miner,
                Version = 0,
                Flags = 0,
                From = from,
                Nonce = nonce,
                Miner = miner
            };
            return tx;
        }

        public Transaction DepositTransaction(UInt160 from, BlockchainType blockchainType, Money value,
            byte[] transactionHash, AddressFormat addressFormat, ulong timestamp)
        {
            var nonce = _transactionRepository.GetTotalTransactionCount(from);
            var deposit = new DepositTransaction
            {
                From = from,
                BlockchainType = blockchainType,
                Value = value.ToUInt256(),
                AddressFormat = addressFormat,
                Timestamp = timestamp,
                TransactionHash = ByteString.CopyFrom(transactionHash)
            };
            var tx = new Transaction
            {
                Type = TransactionType.Deposit,
                Version = 0,
                Flags = 0,
                From = from,
                Nonce = nonce,
                Deposit = deposit
            };
            return tx;
        }
    }
}