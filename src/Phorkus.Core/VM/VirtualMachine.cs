using System;
using System.IO;
using Phorkus.Proto;
using Phorkus.WebAssembly;

namespace Phorkus.Core.VM
{
    using Type = System.Type;
    
    public class VirtualMachine : IVirtualMachine
    {
        public bool VerifyContract(Contract contract)
        {
            var contractCode = contract.Wasm.ToByteArray();
            try
            {
                using (var stream = new MemoryStream(contractCode))
                    Compile.FromBinary<dynamic>(stream);
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
            return false;
        }
        
        public bool InvokeContract(Contract contract, Invocation invocation)
        {
            try
            {
                return _InvokeContractUnsafe(contract, invocation);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return false;
            }
        }
        
        private bool _InvokeContractUnsafe(Contract contract, Invocation invocation)
        {
            if (contract.Version != ContractVersion.Wasm)
                return false;
            
            using (var invocationContext = _CompileWasm<dynamic>(contract.Wasm.ToByteArray())())
            {
                if (!(invocationContext.Exports.GetType() is Type type))
                    return false;
                var method = type.GetMethod(invocation.MethodName);
                if (method is null)
                    return false;
                var result = method.Invoke(invocationContext.Exports, new object[0]);
                Console.WriteLine("Contract result is: " + result);
            }
            return true;
        }
        
        private Func<Instance<T>> _CompileWasm<T>(byte[] buffer)
            where T : class
        {
            using (var stream = new MemoryStream(buffer, 0, buffer.Length, false))
                return Compile.FromBinary<T>(stream);
        }
    }
}