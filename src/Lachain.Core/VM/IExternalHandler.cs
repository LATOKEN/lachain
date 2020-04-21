using System.Collections.Generic;
using WebAssembly.Runtime;

namespace Lachain.Core.VM
{
    public interface IExternalHandler
    {
        ImportDictionary GetFunctionImports();
    }
}