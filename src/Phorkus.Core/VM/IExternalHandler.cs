using System.Collections.Generic;
using WebAssembly.Runtime;

namespace Phorkus.Core.VM
{
    public interface IExternalHandler
    {
        ImportDictionary GetFunctionImports();
    }
}