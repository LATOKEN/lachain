using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Microsoft.Extensions.Options;
using Phorkus.Proto;
using Phorkus.WebAssembly;

namespace Phorkus.Core.VM
{
    using Type = System.Type;
    
    public class VirtualMachine : IVirtualMachine
    {
        private IEnumerable<FunctionImport> _functionImports;
        
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

        private IEnumerable<FunctionImport> GetFunctionImports()
        {
            if (_functionImports != null)
                return _functionImports;
            _functionImports = new EthereumExternalHandler().GetFunctionImports();
            return _functionImports;
        }
        
        private bool _InvokeContractUnsafe(Contract contract, Invocation invocation)
        {
            if (contract.Version != ContractVersion.Wasm)
                return false;
            ContractABI methodAbi = null;
            foreach (var abi in contract.Abi)
            {
                if (!abi.Method.Equals(invocation.MethodName))
                    continue;
                methodAbi = abi;
                break;
            }
            if (methodAbi is null)
                return false;
            using (var invocationContext = _CompileWasm<dynamic>(contract.Wasm.ToByteArray(), GetFunctionImports())())
            {
                if (!(invocationContext.Exports.GetType() is Type type))
                    return false;
                var method = type.GetMethod(invocation.MethodName);
                if (method is null)
                    return false;
                var result = method.Invoke(invocationContext.Exports, _BuildMethodArgs(methodAbi, invocation));
                Console.WriteLine("Contract result is: " + result);
            }
            return true;
        }
        
        private object[] _BuildMethodArgs(ContractABI contractAbi, Invocation invocation)
        {
            var values = invocation.Params.ToArray();
            var valueOffset = 0;
            var result = new List<object>();
            foreach (var type in contractAbi.Input)
            {
                if (type == ContractType.Void)
                    continue;
                switch (type)
                {
                    case ContractType.Signature:
                    case ContractType.Boolean:
                    case ContractType.Hash160:
                    case ContractType.Hash256:
                    case ContractType.ByteArray:
                    case ContractType.PublicKey:
                    case ContractType.String:
                    case ContractType.Array:
                    case ContractType.Void:
                        throw new Exception($"Type ({type}) not supported yet");
                    case ContractType.Integer:
                        result.Add(_ParseInteger(values[valueOffset]));
                        break;
                    case ContractType.Long:
                        result.Add(_ParseLong(values[valueOffset]));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(type), type, null);
                }
                ++valueOffset;
            }
            return result.ToArray();
        }
        
        private int _ParseInteger(ByteString value)
        {
            return BitConverter.ToInt32(value.ToByteArray(), 0);
        }
        
        private long _ParseLong(ByteString value)
        {
            return BitConverter.ToInt64(value.ToByteArray(), 0);
        }

        private Func<Instance<T>> _CompileWasm<T>(byte[] buffer, IEnumerable<RuntimeImport> imports = null)
            where T : class
        {
            using (var stream = new MemoryStream(buffer, 0, buffer.Length, false))
                return Compile.FromBinary<T>(stream, imports);
        }
    }
}