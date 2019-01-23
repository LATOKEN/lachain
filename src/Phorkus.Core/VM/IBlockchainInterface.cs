using System.Collections.Generic;
using Phorkus.WebAssembly;

namespace Phorkus.Core.VM
{
    public interface IBlockchainInterface
    {
        IEnumerable<FunctionImport> GetFunctionImports();
    }
}