using System.Collections.Generic;
using WebAssembly.Runtime;

namespace Lachain.Core.VM
{
    public interface IBlockchainInterface
    {
        ImportDictionary GetFunctionImports();
    }
}