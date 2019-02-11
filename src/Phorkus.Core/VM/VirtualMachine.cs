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
        public ExecutionStatus InvokeContract(Contract contract, UInt160 sender, byte[] input)
        {
            try
            {
                return _InvokeContractUnsafe(contract, sender, input, out _);
            }
            catch (Exception e)
            {
                ExecutionFrames.Clear();
                Console.Error.WriteLine(e);
                return ExecutionStatus.UnknownError;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ExecutionStatus CallContract(Contract contract, UInt160 sender, byte[] input, out byte[] returnValue)
        {
            try
            {
                return _InvokeContractUnsafe(contract, sender, input, out returnValue);
            }
            catch (HaltException e)
            {
                ExecutionFrames.Clear();
                returnValue = new[]
                {
                    (byte) e.HaltCode
                };
                return ExecutionStatus.ExecutionHalted;
            }
            catch (Exception e)
            {
                ExecutionFrames.Clear();
                Console.Error.WriteLine(e);
                returnValue = null;
                return ExecutionStatus.UnknownError;
            }
        }
        
        private static ExecutionStatus _InvokeContractUnsafe(Contract contract, UInt160 sender, byte[] input, out byte[] returnValue)
        {
            returnValue = null;
            if (ExecutionFrames.Count != 0)
                return ExecutionStatus.VmCorruption;
            if (contract.Version != ContractVersion.Wasm)
                return ExecutionStatus.IncompatibleCode;
            var status = ExecutionFrame.FromInvocation(
                contract.Wasm.ToByteArray(),
                sender,
                contract.Hash,
                input,
                BlockchainInterface, out var rootFrame);
            if (status != ExecutionStatus.Ok)
                return status;
            ExecutionFrames.Push(rootFrame);
            var result = rootFrame.Execute();
            if (result == ExecutionStatus.Ok)
                returnValue = rootFrame.ReturnValue;
            ExecutionFrames.Pop();
            return result;
        }
    }
}