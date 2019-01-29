using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Phorkus.Proto;
using Phorkus.Utility.Utils;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Runtime;

// ReSharper disable MemberCanBePrivate.Global

namespace Phorkus.Core.VM
{
    public class EnvExternalHandler : IExternalHandler
    {
        private const string EnvModule = "env";

        private static ExecutionStatus DoInternalCall(UInt160 caller, UInt160 address, byte[] input,
            out ExecutionFrame frame)
        {
            var contract = VirtualMachine.BlockchainSnapshot.Contracts.GetContractByHash(address);
            if (contract is null)
            {
                frame = null;
                return ExecutionStatus.NoSuchContract;
            }

            var status = ExecutionFrame.FromInternalCall(
                contract.Wasm.ToByteArray(), caller, address, input,
                VirtualMachine.BlockchainInterface, out frame
            );
            if (status != ExecutionStatus.Ok) return status;
            VirtualMachine.ExecutionFrames.Push(frame);
            return status;
        }

        private static byte[] SafeCopyFromMemory(UnmanagedMemory memory, int offset, int length)
        {
            if (length < 0 || offset < 0) return null;
            if (offset + length > memory.Size) return null;
            var buffer = new byte[length];
            try
            {
                Marshal.Copy(IntPtr.Add(memory.Start, offset), buffer, 0, length);
            }
            catch (ArgumentNullException)
            {
                return null;
            }

            return buffer;
        }

        private static bool SafeCopyToMemory(UnmanagedMemory memory, byte[] data, int offset)
        {
            if (offset < 0 || offset + data.Length > memory.Size) return false;
            try
            {
                Marshal.Copy(data, 0, IntPtr.Add(memory.Start, offset), data.Length);
            }
            catch (Exception e) when (e is ArgumentNullException || e is ArgumentOutOfRangeException)
            {
                return false;
            }

            return true;
        }

        public static int Handler_Env_GetCallValue(int offset)
        {
            var frame = VirtualMachine.ExecutionFrames.Peek();
            if (offset < 0 || offset >= frame.Input.Length)
            {
                throw new RuntimeException("Bad getcallvalue call");
            }

            return frame.Input[offset];
        }

        public static int Handler_Env_GetCallSize()
        {
            var frame = VirtualMachine.ExecutionFrames.Peek();
            return frame.Input.Length;
        }

        public static void Handler_Env_CopyCallValue(int from, int to, int offset)
        {
            var frame = VirtualMachine.ExecutionFrames.Peek();
            if (from < 0 || to > frame.Input.Length || from > to)
            {
                throw new RuntimeException("Copy to contract memory failed: bad range");
            }
            if (!SafeCopyToMemory(frame.Memory, frame.Input.Skip(from).Take(to - from).ToArray(), offset))
            {
                throw new RuntimeException("Copy to contract memory failed");
            }
        }

        public static void Handler_Env_WriteLog(int offset, int length)
        {
            // TODO: check signs
            var frame = VirtualMachine.ExecutionFrames.Peek();
            var buffer = SafeCopyFromMemory(frame.Memory, offset, length);
            if (buffer == null)
            {
                throw new RuntimeException("Bad call to writelog");
            }
            Console.WriteLine($"Contract ({frame.CurrentAddress}) logged: {buffer.ToHex()}");
        }

        public static int Handler_Env_Call(
            int callSignatureOffset, int inputLength, int inputOffset, int valueOffset, int returnValueOffset)
        {
            var frame = VirtualMachine.ExecutionFrames.Peek();
            var signatureBuffer = SafeCopyFromMemory(frame.Memory, callSignatureOffset, 20);
            var inputBuffer = SafeCopyFromMemory(frame.Memory, inputOffset, inputLength);
            if (signatureBuffer is null || inputBuffer is null)
            {
                throw new RuntimeException("Bad call to call function");
            }
            var address = signatureBuffer.Take(20).ToArray().ToUInt160();
            var status = DoInternalCall(frame.CurrentAddress, address, inputBuffer, out var newFrame);
            if (status != ExecutionStatus.Ok)
            {
                throw new RuntimeException("Cannot invoke call: " + status);
            }

            status = newFrame.Execute(); 
            if (status != ExecutionStatus.Ok)
            {
                throw new RuntimeException("Cannot invoke call: " + status);
            }
            newFrame = VirtualMachine.ExecutionFrames.Pop();
            var returned = newFrame.ReturnValue;
            if (!SafeCopyToMemory(frame.Memory, returned, returnValueOffset))
            {
                throw new RuntimeException("Cannot invoke call: cannot pass return value");
            }
            return 0;
        }

        public static void Handler_Env_StorageLoad(int keyOffset, int valueOffset)
        {
            var frame = VirtualMachine.ExecutionFrames.Peek();
            var key = SafeCopyFromMemory(frame.Memory, keyOffset, 32);
            if (key is null)
            {
                throw new RuntimeException("Bad call to storageload");
            }
            var value = VirtualMachine.BlockchainSnapshot.ContractStorage.GetValue(frame.CurrentAddress, key.ToUInt256());
            if (!SafeCopyToMemory(frame.Memory, value.Buffer.ToByteArray(), valueOffset))
            {
                throw new RuntimeException("Cannot copy storageload result to memory");
            }
        }
        
        public static void Handler_Env_StorageSave(int keyOffset, int valueOffset)
        {
            var frame = VirtualMachine.ExecutionFrames.Peek();
            var key = SafeCopyFromMemory(frame.Memory, keyOffset, 32);
            var value = SafeCopyFromMemory(frame.Memory, valueOffset, 32);
            if (key is null || value is null)
            {
                throw new RuntimeException("Bad call to storageload");
            }
            VirtualMachine.BlockchainSnapshot.ContractStorage.SetValue(frame.CurrentAddress, key.ToUInt256(), value.ToUInt256());
        }
        

        public IEnumerable<FunctionImport> GetFunctionImports()
        {
            return new[]
            {
                new FunctionImport(EnvModule, "getcallvalue",
                    typeof(EnvExternalHandler).GetMethod(nameof(Handler_Env_GetCallValue))),
                new FunctionImport(EnvModule, "getcallsize",
                    typeof(EnvExternalHandler).GetMethod(nameof(Handler_Env_GetCallSize))),
                new FunctionImport(EnvModule, "copycallvalue",
                    typeof(EnvExternalHandler).GetMethod(nameof(Handler_Env_CopyCallValue))),
                new FunctionImport(EnvModule, "call", typeof(EnvExternalHandler).GetMethod(nameof(Handler_Env_Call))),
                new FunctionImport(EnvModule, "writelog",
                    typeof(EnvExternalHandler).GetMethod(nameof(Handler_Env_WriteLog))),
                new FunctionImport(EnvModule, "storageload",
                    typeof(EnvExternalHandler).GetMethod(nameof(Handler_Env_StorageLoad))),
                new FunctionImport(EnvModule, "storagesave",
                    typeof(EnvExternalHandler).GetMethod(nameof(Handler_Env_StorageSave))),
            };
        }
    }
}