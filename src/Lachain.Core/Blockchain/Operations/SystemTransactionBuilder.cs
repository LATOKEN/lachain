using Google.Protobuf;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Crypto;

namespace Lachain.Core.Blockchain.Operations
{
    public class SystemTransactionBuilder : ISystemTransactionBuilder
    {
        private static readonly ILogger<SystemTransactionBuilder> Logger =
            LoggerFactory.GetLoggerForClass<SystemTransactionBuilder>();
        private readonly IBlockManager _blockManager;
        private readonly IStateManager _stateManager;

        public SystemTransactionBuilder(IBlockManager blockManager, IStateManager stateManager)
        {
            _blockManager = blockManager;
            _stateManager = stateManager;
        }

        
        public TransactionReceipt BuildDistributeCycleRewardsAndPenaltiesTxReceipt()
        {
            return BuildSystemContractTxReceipt(ContractRegisterer.GovernanceContract, GovernanceInterface.MethodDistributeCycleRewardsAndPenalties, 
                UInt256Utils.ToUInt256(GovernanceContract.GetCycleByBlockNumber(_blockManager.GetHeight())));
        }
        public bool VerifyDistributeCycleRewardsAndPenaltiesTxReceipt(TransactionReceipt receipt)
        {
            return receipt.Equals(BuildDistributeCycleRewardsAndPenaltiesTxReceipt());
        }

        public TransactionReceipt BuildFinishVrfLotteryTxReceipt()
        {
            return BuildSystemContractTxReceipt(ContractRegisterer.StakingContract,
                StakingInterface.MethodFinishVrfLottery);
        }

        public bool VerifyFinishVrfLotteryTxReceipt(TransactionReceipt receipt)
        {
            return receipt.Equals(BuildFinishVrfLotteryTxReceipt());
        }

        public TransactionReceipt BuildFinishCycleTxReceipt()
        {
            return BuildSystemContractTxReceipt(ContractRegisterer.GovernanceContract, GovernanceInterface.MethodFinishCycle, 
                UInt256Utils.ToUInt256(GovernanceContract.GetCycleByBlockNumber(_blockManager.GetHeight())));
        }

        public bool VerifyFinishCycleTxReceipt(TransactionReceipt receipt)
        {
            return receipt.Equals(BuildFinishCycleTxReceipt());
        }

        private TransactionReceipt BuildSystemContractTxReceipt(UInt160 contractAddress, string mehodSignature, params dynamic[] values)
        {
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(UInt160Utils.Zero);
            var abi = ContractEncoder.Encode(mehodSignature, values);
            var transaction = new Transaction
            {
                To = contractAddress,
                Value = UInt256Utils.Zero,
                From = UInt160Utils.Zero,
                Nonce = nonce,
                GasPrice = 0,
                /* TODO: gas estimation */
                GasLimit = 100000000,
                Invocation = ByteString.CopyFrom(abi),
            };
            return new TransactionReceipt
            {
                Hash = transaction.FullHash(SignatureUtils.Zero),
                Status = TransactionStatus.Pool,
                Transaction = transaction,
                Signature = SignatureUtils.Zero,
            };
        }
    }
}