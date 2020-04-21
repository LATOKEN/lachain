using System.Collections.Generic;
using WebAssembly.Runtime;

namespace Lachain.Core.VM
{
    class BlockchainInterface : IBlockchainInterface
    {
        private ImportDictionary? _functionImports;

        public ImportDictionary GetFunctionImports()
        {
            if (_functionImports != null)
                return _functionImports;
            _functionImports = new ExternalHandler().GetFunctionImports();
            return _functionImports;
        }
    }
}