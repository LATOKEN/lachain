using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface IWithdrawalRunner
    {
        void Start(ThresholdKey thresholdKey, KeyPair keyPair);

        void Stop();
    }
}