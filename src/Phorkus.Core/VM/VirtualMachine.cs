using System;
using System.Collections.Generic;
using System.IO;
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

        // TODO: protection from multiple instantiation

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

        public ExecutionStatus InvokeContract(Contract contract, UInt160 sender, byte[] input)
        {
            try
            {
                return _InvokeContractUnsafe(contract, sender, input);
            }
            catch (Exception e)
            {
                ExecutionFrames.Clear();
                Console.Error.WriteLine(e);
                return ExecutionStatus.UnknownError;
            }
        }

        private static ExecutionStatus _InvokeContractUnsafe(Contract contract, UInt160 sender, byte[] input)
        {
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
            ExecutionFrames.Pop();
            return result;
        }
    }
}