using System;
using System.Collections.Concurrent;
using System.IO;
using Lachain.Core.Blockchain.Error;
using Lachain.Proto;
using WebAssembly;
using WebAssembly.Runtime;

namespace Lachain.Core.Blockchain.VM.ExecutionFrame
{
    public class WasmExecutionFrame : IExecutionFrame
    {
        internal WasmExecutionFrame(
            Instance<JitEntryPoint> compiledInstance,
            InvocationContext invocationContext,
            UInt160 currentAddress,
            byte[] input,
            ulong gasLimit
        )
        {
            CompiledInstance = compiledInstance;
            Exports = CompiledInstance.Exports.GetType();
            if (Exports is null)
                throw new RuntimeException("ill-formed contract binary");
            InvocationContext = invocationContext;
            CurrentAddress = currentAddress;
            Input = input;
            ReturnValue = new byte[] { };
            GasLimit = gasLimit;
        }

        public abstract class JitEntryPoint
        {
            // ReSharper disable once InconsistentNaming
            public abstract void start();
        }

        public Instance<JitEntryPoint> CompiledInstance { get; }
        private Type Exports { get; }

        public UnmanagedMemory Memory
        {
            get
            {
                var memoryGetter = Exports.GetMethod("get_memory");
                return memoryGetter?.Invoke(CompiledInstance.Exports, new object[] { }) as UnmanagedMemory ??
                       throw new InvalidOperationException();
            }
        }

        public InvocationContext InvocationContext { get; }
        public UInt160 CurrentAddress { get; }

        public byte[] ReturnValue { get; set; }
        public byte[] Input { get; }

        public ulong GasLimit { get; }
        public ulong GasUsed { get; private set; }

        public void UseGas(ulong gas)
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
                CompiledInstance.Exports.start();
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
            CompiledInstance?.Dispose();
        }

        // TODO: this is not used properly?
        private static readonly ConcurrentDictionary<UInt160, Func<Instance<JitEntryPoint>>> ByteCodeCache
            = new ConcurrentDictionary<UInt160, Func<Instance<JitEntryPoint>>>();

        internal static Instance<JitEntryPoint> CompileWasm(
            UInt160 contract, byte[] buffer, ImportDictionary imports = null
        )
        {
            if (ByteCodeCache.TryGetValue(contract, out var instance))
                return instance();
            var config = new CompilerConfiguration();
            using var stream = new MemoryStream(buffer, 0, buffer.Length, false);
            return Compile.FromBinary<JitEntryPoint>(stream, config)(imports);
        }
    }
}