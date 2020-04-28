using WebAssembly.Runtime;

namespace Lachain.Core.Blockchain.VM
{
    public interface IBlockchainInterface
    {
        ImportDictionary GetFunctionImports();
    }
}