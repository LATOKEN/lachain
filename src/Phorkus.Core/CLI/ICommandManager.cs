using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.CLI
{
    public interface ICommandManager
    {
        void Start(ThresholdKey thresholdKey, KeyPair keyPair);

        void Stop();

        bool IsWorking { get; }
    }
}