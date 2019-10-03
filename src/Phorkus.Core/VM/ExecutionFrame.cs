using System;
using System.Collections.Concurrent;
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
            Instance<JitEntryPoint> invocationContext,
            InvocationContext context,
            UInt160 currentAddress,
            byte[] input,
            ulong gasLimit)
        {
            InvocationContext = invocationContext;
            Exports = InvocationContext.Exports.GetType();
            if (Exports is null)
                throw new RuntimeException("ill-formed contract binary");
            Context = context;
            CurrentAddress = currentAddress;
            Input = input;
            ReturnValue = new byte[] { };
            GasLimit = gasLimit;
        }

        public abstract class JitEntryPoint
        {
            public abstract void start();
        }

        public static ExecutionStatus FromInvocation(
            byte[] code,
            InvocationContext context,
            UInt160 contract,
            byte[] input,
            IBlockchainInterface blockchainInterface,
            out ExecutionFrame frame,
            ulong gasLimit)
        {
            frame = new ExecutionFrame(
                _CompileWasm(contract, code, blockchainInterface.GetFunctionImports()),
                context, contract, input, gasLimit
            );
            return ExecutionStatus.Ok;
        }

        public static ExecutionStatus FromInternalCall(
            byte[] code,
            InvocationContext context,
            UInt160 currentAddress,
            byte[] input,
            IBlockchainInterface blockchainInterface,
            out ExecutionFrame frame,
            ulong gasLimit)
        {
            frame = new ExecutionFrame(
                _CompileWasm(currentAddress, code, blockchainInterface.GetFunctionImports()),
                context, currentAddress, input, gasLimit
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
                return memoryGetter?.Invoke(InvocationContext.Exports, new object[] { }) as UnmanagedMemory;
            }
        }

        public InvocationContext Context { get; }
        public UInt160 CurrentAddress { get; }

        public byte[] ReturnValue { get; set; }
        public byte[] Input { get; }

        public ulong GasLimit { get; }
        public ulong GasUsed { get; private set; }

        internal void UseGas(ulong gas)
        {
            var gasLimitField = Exports.GetField("💩 GasLimit");
            var currentGas = (ulong) gasLimitField.GetValue(null);
            checked
            {
                currentGas -= gas;
            }
            gasLimitField.SetValue(null, currentGas);
        }

        public ExecutionStatus Execute()
        {
            var method = Exports.GetMethod("start");
            if (method is null)
                return ExecutionStatus.MissingEntry;
            var gasLimitField = Exports.GetField("💩 GasLimit");
            gasLimitField.SetValue(null, GasLimit);
            try
            {
                InvocationContext.Exports.start();
            }
            catch (OverflowException)
            {
                GasUsed = GasLimit - (ulong) gasLimitField.GetValue(null);
                throw new OutOfGasException(GasUsed);
            }
            catch (InvalidProgramException e)
            {
                Console.Error.WriteLine(e);
                GasUsed = GasLimit - (ulong) gasLimitField.GetValue(null);
                return ExecutionStatus.JitCorruption;
            }

            var gasSpent = GasLimit - (ulong) gasLimitField.GetValue(null);
            GasUsed = gasSpent;
            return ExecutionStatus.Ok;
        }

        public void Dispose()
        {
            InvocationContext?.Dispose();
        }
        
        private static readonly ConcurrentDictionary<UInt160, Func<Instance<JitEntryPoint>>> ByteCodeCache
            = new ConcurrentDictionary<UInt160, Func<Instance<JitEntryPoint>>>();
        
        private static Instance<JitEntryPoint> _CompileWasm(UInt160 contract, byte[] buffer, IEnumerable<RuntimeImport> imports = null)
        {
            if (ByteCodeCache.TryGetValue(contract, out var instance))
                return instance();
            using (var stream = new MemoryStream(buffer, 0, buffer.Length, false))
            {
                var func = Compile.FromBinary<JitEntryPoint>(stream, imports);
                //ByteCodeCache.TryAdd(contract, func);
                return func();
            }
        }
    }
}