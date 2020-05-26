using System.Linq;
using System.Reflection;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Blockchain.VM.ExecutionFrame;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.SystemContracts.ContractManager
{
    public class SystemContractCall
    {
        private readonly object _instance;
        private readonly MethodInfo _method;
        private readonly object[] _params;
        private readonly UInt160 _address;

        public SystemContractCall(object instance, MethodInfo method, object[] @params, UInt160 address)
        {
            _method = method;
            _params = @params;
            _address = address;
            _instance = instance;
        }

        public UInt160 GetAddress()
        {
            return _address;
        }

        public ExecutionStatus Invoke(SystemContractExecutionFrame executionFrame)
        {
            return (ExecutionStatus) _method.Invoke(_instance, _params.Append(executionFrame).ToArray());
        }
    }
}