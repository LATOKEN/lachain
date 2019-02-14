using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain
{
    public class TransactionBuilder : ITransactionBuilder
    {
        private readonly IStateManager _stateManager;
        
        public TransactionBuilder(IStateManager stateManager)
        {
            _stateManager = stateManager;
        }

        public Transaction TransferTransaction(UInt160 from, UInt160 to, Money value, byte[] input)
        {
            var nonce = _stateManager.CurrentSnapshot.Transactions.GetTotalTransactionCount(from);
            var tx = new Transaction
            {
                Type = TransactionType.Transfer,
                To = to,
                Value = value.ToUInt256(),
                From = from,
                Fee = _CalcEstimatedBlockFee(),
                Nonce = nonce
            };
            if (input != null)
                tx.Invocation = ByteString.CopyFrom(input);
            return tx;
        }

        public Transaction DeployTransaction(UInt160 from, IEnumerable<byte> byteCode)
        {
            var nonce = _stateManager.CurrentSnapshot.Transactions.GetTotalTransactionCount(from);
            var tx = new Transaction
            {
                Type = TransactionType.Deploy,
                Invocation = ByteString.CopyFrom(),
                Deploy = ByteString.CopyFrom(byteCode.ToArray()),
                From = from,
                Fee = _CalcEstimatedBlockFee(),
                Nonce = nonce
            };
            return tx;
        }

        private UInt256 _CalcEstimatedBlockFee()
        {
            var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(
                _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight());
            return block is null ? UInt256Utils.Zero : _CalcEstimatedBlockFee(block.TransactionHashes).ToUInt256();
        }
        
        private Money _CalcEstimatedBlockFee(IEnumerable<UInt256> txHashes)
        {
            var arrayOfHashes = txHashes as UInt256[] ?? txHashes.ToArray();
            if (arrayOfHashes.Length == 0)
                return Money.Zero;
            var sum = Money.Zero;
            foreach (var txHash in arrayOfHashes)
            {
                var tx = _stateManager.CurrentSnapshot.Transactions.GetTransactionByHash(txHash);
                if (tx is null)
                    continue;
                sum += tx.Transaction.Fee.ToMoney();
            }
            return sum / arrayOfHashes.Length;
        }
    }
}