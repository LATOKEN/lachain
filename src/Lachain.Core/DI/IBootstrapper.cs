using Lachain.Core.CLI;

namespace Lachain.Core.DI
{
    public interface IBootstrapper
    {
        void Start(RunOptions options);
    }
}