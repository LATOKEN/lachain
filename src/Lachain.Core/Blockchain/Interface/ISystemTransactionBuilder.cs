using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface ISystemTransactionBuilder
    {
        TransactionReceipt BuildDistributeCycleRewardsAndPenaltiesTxReceipt();
        bool VerifyDistributeCycleRewardsAndPenaltiesTxReceipt(TransactionReceipt receipt);
        TransactionReceipt BuildFinishVrfLotteryTxReceipt();
        bool VerifyFinishVrfLotteryTxReceipt(TransactionReceipt receipt);
        TransactionReceipt BuildFinishCycleTxReceipt();
        bool VerifyFinishCycleTxReceipt(TransactionReceipt receipt);
    }
}