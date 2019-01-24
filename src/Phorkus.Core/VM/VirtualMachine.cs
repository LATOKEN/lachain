using System;
using System.Collections.Generic;
using System.Data;
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
        public static Stack<VMExecutionFrame> ExecutionFrames { get; } = new Stack<VMExecutionFrame>();

        // TODO: protection from multiple instantiation

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

        private static bool _InvokeContractUnsafe(Contract contract, Invocation invocation)
        {
            if (ExecutionFrames.Count != 0)
                return false;
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
            
            var rootFrame = VMExecutionFrame.FromInvocation(contract.Wasm.ToByteArray(), invocation, new DefaultBlockchainInterface());
            ExecutionFrames.Push(rootFrame);
            if (!(rootFrame.InvocationContext.Exports.GetType() is Type type))
                return false;
            var method = type.GetMethod(invocation.MethodName);
            if (method is null)
                return false;
            var result = method.Invoke(rootFrame.InvocationContext.Exports, new object[]{});
            Console.WriteLine("Contract result is: " + result);
            return true;
        }
    }
}