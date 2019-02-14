using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.CLI
{
    public interface IConsoleManager
    {
        void Start(KeyPair keyPair);

        void Stop();

        bool IsWorking { get; }
    }
}