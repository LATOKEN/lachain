using System;
using System.Collections.Generic;
using System.IO;
using Phorkus.Proto;
using Phorkus.WebAssembly;

namespace Phorkus.Core.VM
{
    public class VMExecutionFrame : IDisposable
    {
        private VMExecutionFrame(Instance<dynamic> invocationContext)
        {
            InvocationContext = invocationContext;
        }

        public static VMExecutionFrame FromInvocation(byte[] code, Invocation invocation, IBlockchainInterface blockchainInterface)
        {
            return new VMExecutionFrame(_CompileWasm<dynamic>(code, blockchainInterface.GetFunctionImports()));
        }

        public static VMExecutionFrame FromInternalCall(byte[] code, Invocation invocation, IBlockchainInterface blockchainInterface)
        {
            return new VMExecutionFrame(_CompileWasm<dynamic>(code, blockchainInterface.GetFunctionImports()));
        }
        
        public Instance<dynamic> InvocationContext { get; }
        public UInt160 Sender { get; }
        public UInt160 Origin { get; }
        public UInt160 Recepient { get; }
        public UInt160 CurrentAddress { get; }
        public byte[] Input { get; }
        public byte[] ReturnValue { get; set; }
        
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