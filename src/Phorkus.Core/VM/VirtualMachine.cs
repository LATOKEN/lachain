using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.WebAssembly;

namespace Phorkus.Core.VM
{
    public class VirtualMachine : IVirtualMachine
    {
        internal static Stack<ExecutionFrame> ExecutionFrames { get; } = new Stack<ExecutionFrame>();
        
        internal static IBlockchainSnapshot BlockchainSnapshot => StateManager.CurrentSnapshot;
        internal static IBlockchainInterface BlockchainInterface { get; } = new BlockchainInterface();
        
        internal static IStateManager StateManager { get; set; }
        internal static ICrypto Crypto { get; set; }

        public VirtualMachine(IStateManager stateManager, ICrypto crypto)
        {
            StateManager = stateManager;
            Crypto = crypto;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool VerifyContract(byte[] contractCode)
        {
            try
            {
                using (var stream = new MemoryStream(contractCode))
                    Compile.FromBinary<dynamic>(stream, BlockchainInterface.GetFunctionImports());
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public InvocationResult InvokeContract(Contract contract, InvocationContext context, byte[] input, ulong gasLimit)
        {
            var executionStatus = ExecutionStatus.Ok;
            byte[] returnValue = null;
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