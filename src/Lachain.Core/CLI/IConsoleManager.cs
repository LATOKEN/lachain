using Lachain.Crypto;
using Lachain.Proto;

namespace Lachain.Core.CLI
{
    public interface IConsoleManager
    {
        void Start(ECDSAKeyPair keyPair);

        void Stop();

        bool IsWorking { get; }
    }
}