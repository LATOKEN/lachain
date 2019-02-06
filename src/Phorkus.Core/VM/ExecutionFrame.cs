﻿using System;
using System.Collections.Generic;
using System.IO;
using Phorkus.Proto;
using Phorkus.Utility.Utils;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Runtime;

namespace Phorkus.Core.VM
{
    public class ExecutionFrame : IDisposable
    {
        private ExecutionFrame(
            Instance<JitEntryPoint> invocationContext, UInt160 sender,
            UInt160 currentAddress, byte[] input)
        {
            InvocationContext = invocationContext;
            Exports = InvocationContext.Exports.GetType() as System.Type;
            if (Exports is null)
                throw new RuntimeException("ill-formed contract binary");
            Sender = sender;
            CurrentAddress = currentAddress;
            Input = input;
            ReturnValue = new byte[] { };
        }

        public abstract class JitEntryPoint
        {
            public abstract void start();
        } 
        
        public static ExecutionStatus FromInvocation(
            byte[] code, UInt160 sender, UInt160 contract, byte[] input, IBlockchainInterface blockchainInterface, out ExecutionFrame frame)
        {
            frame = new ExecutionFrame(
                _CompileWasm<JitEntryPoint>(code, blockchainInterface.GetFunctionImports()),
                sender, contract, input
            );
            return ExecutionStatus.Ok;
        }

        public static ExecutionStatus FromInternalCall(
            byte[] code, UInt160 caller, UInt160 currentAddress, byte[] input,
            IBlockchainInterface blockchainInterface, out ExecutionFrame frame)
        {
            frame = new ExecutionFrame(
                _CompileWasm<JitEntryPoint>(code, blockchainInterface.GetFunctionImports()),
                caller, currentAddress, input
            );
            return ExecutionStatus.Ok;
        }

        public Instance<JitEntryPoint> InvocationContext { get; }
        private System.Type Exports { get; }

        public UnmanagedMemory Memory
        {
            get
            {
                var memoryGetter = Exports.GetMethod("get_memory");
                if (memoryGetter is null)
                    return null;
                return memoryGetter.Invoke(InvocationContext.Exports, new object[] { }) as UnmanagedMemory;
            }
        }

        public UInt160 Sender { get; }
        public UInt160 CurrentAddress { get; }
        
        public byte[] Input { get; }
        public byte[] ReturnValue { get; set; }
        
        public ExecutionStatus Execute()
        {
            var method = Exports.GetMethod("start");
            if (method is null)
                return ExecutionStatus.MissingEntrypoint;
            InvocationContext.Exports.start();
            Console.WriteLine($"Contract {CurrentAddress} exited with return value: {ReturnValue.ToHex()}");
            return ExecutionStatus.Ok;
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