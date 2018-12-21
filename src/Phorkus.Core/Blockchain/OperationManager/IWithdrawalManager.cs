using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public interface IWithdrawalManager
    {
        void AddWithdrawal(Transaction transaction);

        
        void Start(ThresholdKey thresholdKey, KeyPair keyPair);

        void Stop();
    }
}