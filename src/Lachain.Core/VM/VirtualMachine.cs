using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Storage.State;
using WebAssembly;
using WebAssembly.Runtime;

namespace Lachain.Core.VM
{
    public class VirtualMachine : IVirtualMachine
    {
        internal static Stack<ExecutionFrame> ExecutionFrames { get; } = new Stack<ExecutionFrame>();

        internal static IBlockchainSnapshot? BlockchainSnapshot => StateManager?.CurrentSnapshot;
        internal static IBlockchainInterface BlockchainInterface { get; } = new BlockchainInterface();

        private static IStateManager StateManager { get; set; }

        internal static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        public VirtualMachine(IStateManager stateManager)
        {
            StateManager = stateManager;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool VerifyContract(byte[] contractCode)
        {
            try
            {
                var config = new CompilerConfiguration();
                using (var stream = new MemoryStream(contractCode))
                    Compile.FromBinary<dynamic>(stream, config)(BlockchainInterface.GetFunctionImports());
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public InvocationResult InvokeContract(Contract contract, InvocationContext context, byte[] input,
            ulong gasLimit)
        {
            var executionStatus = ExecutionStatus.Ok;
            var returnValue = new byte[] { };
            var gasUsed = 0UL;
            try
            {
                if (ExecutionFrames.Count != 0)
                    return InvocationResult.FactoryDefault(ExecutionStatus.VmStackCorruption);
                var status = ExecutionFrame.FromInvocation(
                    contract.ByteCode.ToByteArray(),
                    context,
                    contract.ContractAddress,
                    input,
                    BlockchainInterface,
                    out var rootFrame,
                    gasLimit);
                if (status != ExecutionStatus.Ok)
                    return InvocationResult.FactoryDefault(status);
                ExecutionFrames.Push(rootFrame);
                var result = rootFrame.Execute();
                if (result == ExecutionStatus.Ok)
                    returnValue = rootFrame.ReturnValue;
                gasUsed = rootFrame.GasUsed;
                ExecutionFrames.Pop();
            }
            catch (OutOfGasException e)
            {
                ExecutionFrames.Clear();
                gasUsed = e.GasUsed;
                executionStatus = ExecutionStatus.GasOverflow;
            }
            catch (HaltException e)
            {
                ExecutionFrames.Clear();
                returnValue = new[]
                {
                    (byte) e.HaltCode
                };
                executionStatus = ExecutionStatus.ExecutionHalted;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                ExecutionFrames.Clear();
                executionStatus = ExecutionStatus.UnknownError;
            }

            return new InvocationResult
            {
                GasUsed = gasUsed,
                Status = executionStatus,
                ReturnValue = returnValue
            };
        }
    }
}