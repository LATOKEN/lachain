using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Phorkus.Utility.Utils;
using Phorkus.WebAssembly;

// ReSharper disable MemberCanBePrivate.Global
// disable inspection since methods must be public and static for binding in WASM

namespace Phorkus.Core.VM
{
    public class EnvExternalHandler : IExternalHandler
    {
        private const string EnvModule = "env";

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
            
            Console.WriteLine($"Contract ({frame.CurrentAddress}) logged: {System.Text.Encoding.ASCII.GetString(buffer)}");
        }

        public static int Handler_Env_Call(
            int callSignatureOffset, int inputLength, int inputOffset, int valueOffset, int returnValueOffset
        )
        {
            try
            {
                // TODO: check signs
                var frame = VirtualMachine.ExecutionFrames.Peek();
                var signatureBuffer = new byte[24];
                Marshal.Copy(IntPtr.Add(frame.Memory.Start, callSignatureOffset), signatureBuffer, 0, 24);
                var address = signatureBuffer.Take(20).ToArray().ToUInt160();
                var methodSig = signatureBuffer.Skip(20).ToArray(); // TODO: methods sigs
                var contract = VirtualMachine.BlockchainSnapshot.Contracts.GetContractByHash(address);
                if (contract is null)
                {
                    return 2; // TODO: exit codes are weird
                }

                var inputBuffer = new byte[inputLength];
                Marshal.Copy(IntPtr.Add(frame.Memory.Start, inputOffset), inputBuffer, 0, inputLength);
                
                if (ExecutionFrame.FromInternalCall(out var newFrame) != ExecutionStatus.OK)
                    return 1;
                VirtualMachine.ExecutionFrames.Push(newFrame);
                if (newFrame.Execute() != ExecutionStatus.OK)
                {
                    return 2;
                }
                newFrame = VirtualMachine.ExecutionFrames.Pop();
                var returned = newFrame.ReturnValue;
                Marshal.Copy(returned, 0, IntPtr.Add(frame.Memory.Start, returnValueOffset), returned.Length);
            }
            catch (Exception e)
            {
                return 3;
            }
            
            return 0;
        }

        public IEnumerable<FunctionImport> GetFunctionImports()
        {
            return new[]
            {
                new FunctionImport(EnvModule, "getcallvalue", typeof(EnvExternalHandler).GetMethod(nameof(Handler_Env_GetCallValue))),
                new FunctionImport(EnvModule, "call", typeof(EnvExternalHandler).GetMethod(nameof(Handler_Env_Call))),
                new FunctionImport(EnvModule, "log", typeof(EnvExternalHandler).GetMethod(nameof(Handler_Env_Log))),
            };
        }
    }
}