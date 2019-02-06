using System.Collections.Generic;
using Phorkus.WebAssembly;

namespace Phorkus.Core.VM
{
    class DefaultBlockchainInterface : IBlockchainInterface
    {
        private IEnumerable<FunctionImport> _functionImports;

        public IEnumerable<FunctionImport> GetFunctionImports()
        {
            if (_functionImports != null)
                return _functionImports;
            _functionImports = new ExternalHandler().GetFunctionImports();
            return _functionImports;
        }
    }
}