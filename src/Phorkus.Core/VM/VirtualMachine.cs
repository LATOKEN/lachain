using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Phorkus.Proto;
using Phorkus.Utility.Utils;
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
                        result.AddRange(_ParseSignature(values[valueOffset]));
                        break;
                    case ContractType.Boolean:
                        result.Add(_ParseBoolean(values[valueOffset]));
                        break;
                    case ContractType.Int160:
                        result.AddRange(_ParseInt160(values[valueOffset]));
                        break;
                    case ContractType.Int256:
                        result.AddRange(_ParseInt256(values[valueOffset]));
                        break;
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

        private object[] _ParseSignature(ByteString value)
        {
            if (value.Length != 65)
                throw new ArgumentOutOfRangeException(nameof(value), "Signature value must contains 65 bytes");
            var bytes = value.ToByteArray();
            var result = new object[9];
            for (var i = 0; i < result.Length; i++)
                result[i] = BitConverter.ToInt64(bytes, i * 8);
            return result;
        }

        private object[] _ParseInt160(ByteString value)
        {
            if (value.Length != 20)
                throw new ArgumentOutOfRangeException(nameof(value), "UInt160 value must contains 20 bytes");
            var bytes = value.ToByteArray();
            var result = new object[3];
            for (var i = 0; i < result.Length; i++)
                result[i] = BitConverter.ToInt64(bytes, i * 8);
            return result;
        }
        
        private object[] _ParseInt256(ByteString value)
        {
            if (value.Length != 32)
                throw new ArgumentOutOfRangeException(nameof(value), "UInt256 value must contains 32 bytes");
            var bytes = value.ToByteArray();
            var result = new object[4];
            for (var i = 0; i < result.Length; i++)
                result[i] = BitConverter.ToInt64(bytes, i * 8);
            return result;
        }
        
        private object _ParseBoolean(ByteString value)
        {
            if (value.Length != 1)
                throw new ArgumentOutOfRangeException(nameof(value), "Boolean value must contains 1 byte");
            return BitConverter.ToBoolean(value.ToByteArray(), 0);
        }
        
        private object _ParseInteger(ByteString value)
        {
            if (value.Length != 4)
                throw new ArgumentOutOfRangeException(nameof(value), "Integer value must contains 4 bytes");
            return BitConverter.ToInt32(value.ToByteArray(), 0);
        }
        
        private object _ParseLong(ByteString value)
        {
            if (value.Length != 8)
                throw new ArgumentOutOfRangeException(nameof(value), "Long value must contains 8 bytes");
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