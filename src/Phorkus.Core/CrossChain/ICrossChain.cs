using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.CrossChain
{
    public interface ICrossChain
    {
        bool IsWorking { get; }

        void Start(ThresholdKey thresholdKey, KeyPair keyPair);

        void Stop();
    }
}