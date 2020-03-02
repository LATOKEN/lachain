using System.Collections.Generic;
using WebAssembly.Runtime;

namespace Phorkus.Core.VM
{
    public interface IBlockchainInterface
    {
        ImportDictionary GetFunctionImports();
    }
}