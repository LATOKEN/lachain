using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility;

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
                GasPrice = _CalcEstimatedBlockFee(),
                GasLimit = 21_000,
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
                GasPrice = _CalcEstimatedBlockFee(),
                /* TODO: "calculate gas limit for input size" */
                GasLimit = 200_000,
                Nonce = nonce
            };
            return tx;
        }
        
        private ulong _CalcEstimatedBlockFee()
        {
            var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(
                _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight());
            return block is null ? 0 : _CalcEstimatedBlockFee(block.TransactionHashes);
        }
        
        private ulong _CalcEstimatedBlockFee(IEnumerable<UInt256> txHashes)
        {
            var arrayOfHashes = txHashes as UInt256[] ?? txHashes.ToArray();
            if (arrayOfHashes.Length == 0)
                return 0;
            var sum = arrayOfHashes.Select(txHash => _stateManager.CurrentSnapshot.Transactions.GetTransactionByHash(txHash))
                .Where(tx => !(tx is null))
                .Aggregate(0UL, (current, tx) => current + tx.Transaction.GasPrice);
            return sum / (ulong) arrayOfHashes.Length;
        }
    }
}