using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Google.Protobuf;
using Phorkus.Proto;
using Phorkus.Utility.Utils;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Runtime;

// ReSharper disable MemberCanBePrivate.Global
// disable inspection since methods must be public and static for binding in WASM

namespace Phorkus.Core.VM
{
    public class EnvExternalHandler : IExternalHandler
    {
        private const string EnvModule = "env";

        private static ExecutionStatus DoInternalCall(UInt160 caller, UInt160 address, int signature, byte[] input,
            out ExecutionFrame frame)
        {
            var contract = VirtualMachine.BlockchainSnapshot.Contracts.GetContractByHash(address);
            if (contract is null)
            {
                frame = null;
                return ExecutionStatus.IncorrectCall;
            }

            var status = ExecutionFrame.FromInternalCall(
                contract.Wasm.ToByteArray(), signature, caller, address, input,
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
            return 0;
        }

        public static void Handler_Env_Log(int offset, int length)
        {
            // TODO: check signs
            var frame = VirtualMachine.ExecutionFrames.Peek();
            var buffer = new byte[length];
            Marshal.Copy(IntPtr.Add(frame.Memory.Start, offset), buffer, 0, length);

            Console.WriteLine(
                $"Contract ({frame.CurrentAddress}) logged: {System.Text.Encoding.ASCII.GetString(buffer)}");
        }

        public static int Handler_Env_Call(
            int callSignatureOffset, int inputLength, int inputOffset, int valueOffset, int returnValueOffset
        )
        {
            try
            {
                var frame = VirtualMachine.ExecutionFrames.Peek();
                var signatureBuffer = SafeCopyFromMemory(frame.Memory, callSignatureOffset, 24);
                var inputBuffer = SafeCopyFromMemory(frame.Memory, inputOffset, inputLength);
                if (signatureBuffer is null || inputBuffer is null)
                    return 2; // TODO: return values need reconsideration

                var address = signatureBuffer.Take(20).ToArray().ToUInt160();
                var methodSig = signatureBuffer.Skip(20).ToArray();
                if (DoInternalCall(frame.CurrentAddress, address, BitConverter.ToInt32(methodSig, 0), inputBuffer,
                        out var newFrame) != ExecutionStatus.Ok)
                {
                    return 2;
                }

                if (newFrame.Execute() != ExecutionStatus.Ok)
                {
                    return 2;
                }

                newFrame = VirtualMachine.ExecutionFrames.Pop();
                var returned = newFrame.ReturnValue;
                if (!SafeCopyToMemory(frame.Memory, returned, returnValueOffset))
                {
                    return 2; // TODO: need to revert everything?
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 3;
            }

            return 0;
        }

        public IEnumerable<FunctionImport> GetFunctionImports()
        {
            return new[]
            {
                new FunctionImport(EnvModule, "getcallvalue",
                    typeof(EnvExternalHandler).GetMethod(nameof(Handler_Env_GetCallValue))),
                new FunctionImport(EnvModule, "call", typeof(EnvExternalHandler).GetMethod(nameof(Handler_Env_Call))),
                new FunctionImport(EnvModule, "log", typeof(EnvExternalHandler).GetMethod(nameof(Handler_Env_Log))),
            };
        }
    }
}