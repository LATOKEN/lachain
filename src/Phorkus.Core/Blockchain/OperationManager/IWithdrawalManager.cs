using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public interface IWithdrawalManager
    {
        Withdrawal CreateWithdrawal(Transaction transaction);
        
        OperatingError Verify(Withdrawal withdrawal);

        void ConfirmWithdrawal(KeyPair keyPair, ulong nonce);

        void ExecuteWithdrawal(ThresholdKey thresholdKey, KeyPair keyPair, ulong nonce);

        ulong CurrentNoncePending { get; }
        ulong CurrentNonceSent { get; }
        /*
        void Start(ThresholdKey thresholdKey, KeyPair keyPair);

        void Stop();*/
    }
}