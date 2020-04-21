using Lachain.Crypto.ECDSA;

namespace Lachain.Core.CLI
{
    public interface IConsoleManager
    {
        void Start(EcdsaKeyPair keyPair);

        void Stop();

        bool IsWorking { get; }
    }
}