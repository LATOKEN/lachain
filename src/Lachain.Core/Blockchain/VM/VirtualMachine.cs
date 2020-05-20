using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.VM.ExecutionFrame;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Storage.State;
using WebAssembly.Runtime;

namespace Lachain.Core.Blockchain.VM
{
    public class VirtualMachine : IVirtualMachine
    {
        internal static Stack<IExecutionFrame> ExecutionFrames { get; } = new Stack<IExecutionFrame>();

        internal static IBlockchainSnapshot? BlockchainSnapshot => StateManager?.CurrentSnapshot;
        internal static IBlockchainInterface BlockchainInterface { get; } = new BlockchainInterface();

        private static IStateManager? StateManager { get; set; }

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
            catch (Exception)
            {
                return false;
            }
        }

        public InvocationResult InvokeSystemContract(
            SystemContractCall systemContractCall, InvocationContext context, byte[] input, ulong gasLimit
        )
        {
            if (ExecutionFrames.Count != 0)
                return InvocationResult.WithStatus(ExecutionStatus.VmStackCorruption);
            var status = FrameFactory.FromSystemContractCall(
                systemContractCall,
                context,
                input,
                out var rootFrame,
                gasLimit
            );
            return status == ExecutionStatus.Ok ? ExecuteFrame(rootFrame) : InvocationResult.WithStatus(status);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public InvocationResult InvokeContract(
            Contract contract, InvocationContext context, byte[] input, ulong gasLimit
        )
        {
            if (ExecutionFrames.Count != 0)
                return InvocationResult.WithStatus(ExecutionStatus.VmStackCorruption);
            var status = FrameFactory.FromInvocation(
                contract.ByteCode.ToByteArray(),
                context,
                contract.ContractAddress,
                input,
                BlockchainInterface,
                out var rootFrame,
                gasLimit
            );
            return status == ExecutionStatus.Ok ? ExecuteFrame(rootFrame) : InvocationResult.WithStatus(status);
        }

        private InvocationResult ExecuteFrame(IExecutionFrame frame)
        {
            var executionStatus = ExecutionStatus.Ok;
            var returnValue = new byte[] { };
            var gasUsed = 0UL;
            try
            {
                ExecutionFrames.Push(frame);
                var result = frame.Execute();
                if (result == ExecutionStatus.Ok)
                    returnValue = frame.ReturnValue;
                gasUsed = frame.GasUsed;
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