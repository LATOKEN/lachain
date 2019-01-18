using System.Collections.Generic;
using Phorkus.WebAssembly;

namespace Phorkus.Core.VM
{
    public interface IExternalHandler
    {
        IEnumerable<FunctionImport> GetFunctionImports();
    }
}