using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.VM;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain
{
    public class TransactionBuilder : ITransactionBuilder
    {
        private static readonly ILogger<TransactionBuilder> Logger =
            LoggerFactory.GetLoggerForClass<TransactionBuilder>();

        private readonly IStateManager _stateManager;
        private readonly ITransactionPool _transactionPool;

        public TransactionBuilder(IStateManager stateManager, ITransactionPool transactionPool)
        {
            _stateManager = stateManager;
            _transactionPool = transactionPool;
        }

        public ulong GetCurrentNonceForAddress(UInt160 address)
        {
            var poolNonce = _transactionPool.GetMaxNonceForAddress(address);
            var stateNonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(address);
            return poolNonce.HasValue ? Math.Max(poolNonce.Value + 1, stateNonce) : stateNonce;
        }

        public Transaction TransferTransaction(UInt160 from, UInt160 to, Money value, byte[]? input)
        {
            var nonce = GetCurrentNonceForAddress(from);
            var tx = new Transaction
            {
                To = to,
                Value = value.ToUInt256(),
                From = from,
                GasPrice = _CalcEstimatedBlockFee(),
                GasLimit = GasMetering.DefaultBlockGasLimit,
                Nonce = nonce
            };
            if (input != null)
                tx.Invocation = ByteString.CopyFrom(input);
            return tx;
        }

        public Transaction DeployTransaction(UInt160 from, IEnumerable<byte> byteCode, byte[]? input)
        {
            // TODO: fix this
            var nonce = GetCurrentNonceForAddress(from);
            var tx = new Transaction
            {
                Invocation = ByteString.CopyFrom(input ?? new byte[0]),
                From = from,
                GasPrice = _CalcEstimatedBlockFee(),
                /* TODO: "calculate gas limit for input size" */
                GasLimit = GasMetering.DefaultBlockGasLimit,
                Nonce = nonce
            };
            return tx;
        }

        public Transaction TokenTransferTransaction(UInt160 contract, UInt160 from, UInt160 to, Money value)
        {
            var nonce = GetCurrentNonceForAddress(from);
            var abi = ContractEncoder.Encode("transfer(address,uint256)", to, value.ToUInt256());
            var tx = new Transaction
            {
                To = contract,
                Invocation = ByteString.CopyFrom(abi),
                From = from,
                GasPrice = _CalcEstimatedBlockFee(),
                /* TODO: "calculate gas limit for input size" */
                GasLimit = GasMetering.DefaultBlockGasLimit,
                Nonce = nonce,
                Value = UInt256Utils.Zero
            };
            return tx;
        }

        public Transaction InvokeTransaction(UInt160 from, UInt160 contract, Money value, string methodSignature,
            params dynamic[] values)
        {
            var nonce = GetCurrentNonceForAddress(from);
            var abi = ContractEncoder.Encode(methodSignature, values);
            var tx = new Transaction
            {
                To = contract,
                Invocation = ByteString.CopyFrom(abi),
                From = from,
                GasPrice = _CalcEstimatedBlockFee(),
                /* TODO: "calculate gas limit for input size" */
                GasLimit = GasMetering.DefaultBlockGasLimit,
                Nonce = nonce,
                Value = value.ToUInt256()
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
            var sum = arrayOfHashes.SelectMany(txHash =>
                {
                    var tx = _stateManager.CurrentSnapshot.Transactions.GetTransactionByHash(txHash);
                    return tx is null ? Enumerable.Empty<TransactionReceipt>() : new[] {tx};
                })
                .Aggregate(0UL, (current, tx) => current + tx.Transaction.GasPrice);
            return sum / (ulong) arrayOfHashes.Length;
        }
    }
}