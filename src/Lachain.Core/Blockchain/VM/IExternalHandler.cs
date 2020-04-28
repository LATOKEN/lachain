using WebAssembly.Runtime;

namespace Lachain.Core.Blockchain.VM
{
    public interface IExternalHandler
    {
        ImportDictionary GetFunctionImports();
    }
}