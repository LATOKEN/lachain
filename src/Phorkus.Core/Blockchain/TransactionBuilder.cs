using System.Globalization;
using Google.Protobuf;
using Phorkus.Proto;
using Phorkus.Storage.RocksDB.Repositories;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain
{
    public class TransactionBuilder : ITransactionBuilder
    {
        private readonly ITransactionRepository _transactionRepository;

        public TransactionBuilder(ITransactionRepository transactionRepository)
        {
            _transactionRepository = transactionRepository;
        }

        public Transaction RegisterTransaction(AssetType type, string name, Money supply, uint decimals, UInt160 owner)
        {
            var nonce = _transactionRepository.GetTotalTransactionCount(UInt160Utils.Zero);
            var registerTx = new RegisterTransaction
            {
                Type = type,
                Name = name,
                Supply = supply.ToUInt256(),
                Decimals = decimals,
                Owner = owner
            };
            var tx = new Transaction
            {
                Type = TransactionType.Register,
                Version = 0,
                Flags = (ulong) TransactionFlag.None,
                From = UInt160Utils.Zero,
                Register = registerTx,
                Nonce = nonce
            };
            return tx;
        }


        public Transaction ContractTransaction(UInt160 from, UInt160 to, Asset asset, Money value, Money fee,
            byte[] script)
        {
            var nonce = _transactionRepository.GetTotalTransactionCount(from);
            var contractTx = new ContractTransaction
            {
                Asset = asset.Hash,
                To = to,
                Value = value.ToUInt256(),
                Script = script == null ? new UInt160().ToByteString() : script.ToUInt160().ToByteString(),
                Fee = fee.ToUInt256()
            };
            var tx = new Transaction
            {
                Type = TransactionType.Contract,
                Version = 0,
                Flags = (ulong) TransactionFlag.None,
                From = from,
                Contract = contractTx,
                Nonce = nonce
            };
            return tx;
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

        public Transaction DepositTransaction(UInt160 from, UInt160 recipient, BlockchainType blockchainType,
            Money value,
            byte[] transactionHash, AddressFormat addressFormat, ulong timestamp)
        {
            var nonce = _transactionRepository.GetTotalTransactionCount(recipient);
            var deposit = new DepositTransaction
            {
                Recipient = recipient,
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


        public Transaction WithdrawTransaction(UInt160 from, UInt160 recipient, BlockchainType blockchainType,
            Money value, byte[] transactionHash, AddressFormat addressFormat, ulong timestamp)
        {
            var nonce = _transactionRepository.GetTotalTransactionCount(recipient);
            var withdraw = new WithdrawTransaction
            {
                Recipient = recipient,
                BlockchainType = blockchainType,
                Value = value.ToUInt256(),
                AddressFormat = addressFormat,
                Timestamp = timestamp,
                TransactionHash = ByteString.CopyFrom(transactionHash)
            };
            var tx = new Transaction
            {
                Type = TransactionType.Withdraw,
                Version = 0,
                Flags = 0,
                From = from,
                Nonce = nonce,
                Withdraw = withdraw
            };
            return tx;
        }


        public Transaction ConfirmTransaction(UInt160 from, UInt160 recipient, BlockchainType blockchainType,
            Money value, byte[] transactionHash, AddressFormat addressFormat, ulong timestamp)
        {
            var nonce = _transactionRepository.GetTotalTransactionCount(recipient);
            var confirm = new ConfirmTransaction
            {
                Recipient = recipient,
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
                Confirm = confirm
            };
            return tx;
        }
    }
}