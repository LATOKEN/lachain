using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public interface IWithdrawalManager
    {
        Withdrawal CreateWithdrawal(Transaction transaction);
        
        OperatingError Verify(Withdrawal withdrawal);

        void ConfirmWithdrawal(Withdrawal withdrawal, byte[] transactionHash, KeyPair keyPair);
        
        void Start(ThresholdKey thresholdKey, KeyPair keyPair);

        void Stop();
    }
}