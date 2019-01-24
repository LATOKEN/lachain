using System;
using System.Collections.Generic;
using System.IO;
using Phorkus.Proto;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Runtime;

namespace Phorkus.Core.VM
{
    public class ExecutionFrame : IDisposable
    {
        private ExecutionFrame(
            Instance<dynamic> invocationContext, string method, UInt160 sender, UInt160 origin, UInt160 recepient,
            UInt160 currentAddress, byte[] input)
        {
            InvocationContext = invocationContext;
            Exports = InvocationContext.Exports.GetType() as System.Type;
            if (Exports is null) throw new RuntimeException("ill-formed contract binary");
            Method = method;
            Sender = sender;
            Origin = origin;
            Recepient = recepient;
            CurrentAddress = currentAddress;
            Input = input;
            ReturnValue = new byte[] { };
        }

        public static ExecutionStatus FromInvocation(
            byte[] code, Invocation invocation,
            IBlockchainInterface blockchainInterface, out ExecutionFrame frame)
        {
            frame = new ExecutionFrame(
                _CompileWasm<dynamic>(code, blockchainInterface.GetFunctionImports()),
                invocation.MethodName,
                invocation.Sender,
                invocation.Sender,
                invocation.ContractAddress,
                invocation.ContractAddress,
                invocation.Input.ToByteArray()
            );
            return ExecutionStatus.OK;
        }

        public static ExecutionStatus FromInternalCall(out ExecutionFrame frame)
        {
            frame = null;
            return ExecutionStatus.OK;
        }

        public Instance<dynamic> InvocationContext { get; }
        private System.Type Exports { get; }

        public UnmanagedMemory Memory
        {
            get
            {
                var memoryGetter = Exports.GetMethod("get_memory");
                if (memoryGetter is null) return null;
                return memoryGetter.Invoke(InvocationContext.Exports, new object[] { }) as UnmanagedMemory;
            }
        }

        public string Method;
        public UInt160 Sender { get; }
        public UInt160 Origin { get; }
        public UInt160 Recepient { get; }
        public UInt160 CurrentAddress { get; }
        public byte[] Input { get; }
        public byte[] ReturnValue { get; set; }

        public ExecutionStatus Execute()
        {
            var method = Exports.GetMethod(Method);
            if (method is null)
                return ExecutionStatus.MISSING_SYMBOL;
            var result = method.Invoke(InvocationContext.Exports, new object[] { });
            Console.WriteLine("Contract result is: " + result);
            return ExecutionStatus.OK;
        }

        public void Dispose()
        {
            InvocationContext?.Dispose();
        }

        private static Instance<T> _CompileWasm<T>(byte[] buffer, IEnumerable<RuntimeImport> imports = null)
            where T : class
        {
            using (var stream = new MemoryStream(buffer, 0, buffer.Length, false))
                return Compile.FromBinary<T>(stream, imports)();
        }
    }
}