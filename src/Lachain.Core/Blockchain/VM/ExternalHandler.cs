using System;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using Google.Protobuf;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.SystemContracts.Utils;
using Lachain.Core.Blockchain.VM.ExecutionFrame;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using WebAssembly.Runtime;

namespace Lachain.Core.Blockchain.VM
{
    public class ExternalHandler : IExternalHandler
    {
        private static readonly ILogger<ExternalHandler> Logger = LoggerFactory.GetLoggerForClass<ExternalHandler>();
        private const string EnvModule = "env";

        private static InvocationResult DoInternalCall(
            UInt160 caller,
            UInt160 address,
            byte[] input,
            ulong gasLimit,
            InvocationMessage message)
        {
            Logger.LogInformation($"DoInternalCall({caller.ToHex()}, {address.ToHex()}, {input.ToHex()}, {gasLimit})");
            var currentFrame = VirtualMachine.ExecutionFrames.Peek();
            var context = currentFrame.InvocationContext.NextContext(caller);
            context.Message = message;
            return ContractInvoker.Invoke(address, context, input, gasLimit);
        }
        
        private static ulong GetDeployHeight(UInt160 contract, UInt160 caller, ulong gasLimit, InvocationMessage message)
        {
            var input = ContractEncoder.Encode(DeployInterface.MethodGetDeployHeight, contract);
            var height = DoInternalCall(caller, ContractRegisterer.DeployContract,
                input,  gasLimit,  message).ReturnValue!;
            return BitConverter.ToUInt64(height, 0);
        }
        
        private static void SetDeployHeight(UInt160 contract, ulong height, UInt160 caller, ulong gasLimit, InvocationMessage message)
        {
            var input = ContractEncoder.Encode(DeployInterface.MethodSetDeployHeight, contract,  height.ToBytes());
            DoInternalCall(caller, ContractRegisterer.DeployContract, input, gasLimit, message);
        }

        private static byte[]? SafeCopyFromMemory(UnmanagedMemory memory, int offset, int length)
        {
            Logger.LogInformation($"SafeCopyFromMemory({memory.Size}, {offset}, {length})");
            var frame = VirtualMachine.ExecutionFrames.Peek();
            if (length < 0 || offset < 0)
                return null;
            if (offset + length > memory.Size) 
                return null;
            frame.UseGas(GasMetering.CopyFromMemoryGasPerByte * (ulong) length);
            var buffer = new byte[length];
            try
            {
                Marshal.Copy(IntPtr.Add(memory.Start, offset), buffer, 0, length);
                Logger.LogInformation($"Result: {buffer.ToHex()}");
            }
            catch (ArgumentNullException)
            {
                return null;
            }

            return buffer;
        }

        private static bool SafeCopyToMemory(UnmanagedMemory memory, byte[] data, int offset)
        {
            Logger.LogInformation($"SafeCopyToMemory({memory.Size}, {data.ToHex()}, {offset})");
            var frame = VirtualMachine.ExecutionFrames.Peek();
            if (offset < 0 || offset + data.Length > memory.Size)
                return false;
            frame.UseGas(GasMetering.CopyToMemoryGasPerByte * (ulong) data.Length);
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

        private static int InvokeContract(
            int callSignatureOffset, int inputLength, int inputOffset, int valueOffset, int gasOffset, InvocationType invocationType)
        {
            Logger.LogInformation($"InvokeContract({callSignatureOffset}, {inputLength}, {inputOffset}, {valueOffset}, {gasOffset}, {invocationType})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call InvokeContract outside wasm frame");
            var snapshot = frame.InvocationContext.Snapshot;
            var addressBuffer = SafeCopyFromMemory(frame.Memory, callSignatureOffset, 20);
            var inputBuffer = SafeCopyFromMemory(frame.Memory, inputOffset, inputLength);
            if (addressBuffer is null || inputBuffer is null)
                throw new InvalidContractException("Bad call to call function");
            var address = addressBuffer.ToUInt160();
            Logger.LogInformation($"Address: {address.ToHex()}");
            var msgValue = SafeCopyFromMemory(frame.Memory, valueOffset, 32)?.ToUInt256();
            var value = msgValue!.ToMoney();
            Logger.LogInformation($"Value: {value}");

            if (value is null)
                throw new InvalidContractException("Bad call to call function");
            if (value > Money.Zero)
            {
                if (frame.InvocationContext.Message?.Type == InvocationType.Static)
                {
                    throw new InvalidOperationException("Cannot call call with non-zero value in STATICCALL");
                }

                frame.UseGas(GasMetering.TransferFundsGasCost);
                var result = snapshot.Balances.TransferBalance(frame.CurrentAddress, address, value);
                if (!result)
                    throw new InsufficientFundsException();
            }

            if (snapshot.Contracts.GetContractByHash(address) is null) {
                frame.LastChildReturnValue = Array.Empty<byte>();
                return 0;
            }

            var gasBuffer = SafeCopyFromMemory(frame.Memory, gasOffset, 8);
            if (gasBuffer is null)
                throw new InvalidContractException("Bad call to call function");
            var gasLimit = gasBuffer.AsReadOnlySpan().ToUInt64();
            if (gasLimit == 0 || gasLimit > frame.GasLimit - frame.GasUsed)
                gasLimit = frame.GasLimit - frame.GasUsed;

            InvocationMessage invocationMessage = new InvocationMessage {
                Type = (frame.InvocationContext.Message?.Type == InvocationType.Static ? InvocationType.Static : invocationType),
            };

            switch (invocationType)
            {
                case InvocationType.Static:
                case InvocationType.Regular:
                    invocationMessage.Sender = frame.InvocationContext.Message?.Delegate ?? frame.CurrentAddress;
                    invocationMessage.Value = msgValue;

                    break;

                case InvocationType.Delegate:
                    invocationMessage.Sender = frame.InvocationContext.Message?.Sender ?? frame.InvocationContext.Sender;
                    invocationMessage.Value = frame.InvocationContext.Message?.Value ?? frame.InvocationContext.Value;
                    invocationMessage.Delegate = frame.InvocationContext.Message?.Delegate ?? frame.CurrentAddress;

                    break;
            }
            Logger.LogInformation($"invocationMessage.Sender: {invocationMessage.Sender.ToHex()}");
            var callResult = DoInternalCall(frame.CurrentAddress, address, inputBuffer, gasLimit, invocationMessage);
            if (callResult.Status != ExecutionStatus.Ok)
            {
                throw new InvalidContractException($"Cannot invoke call: {callResult.Status}, {callResult.ReturnValue}");
            }

            frame.UseGas(callResult.GasUsed);
            frame.LastChildReturnValue = callResult.ReturnValue ?? Array.Empty<byte>();
            return 0;
        }

        public static int Handler_Env_GetCallValue(int offset)
        {
            Logger.LogInformation($"Handler_Env_GetCallValue({offset})");
            var frame = VirtualMachine.ExecutionFrames.Peek();
            frame.UseGas(GasMetering.GetCallValueGasCost);
            if (offset < 0 || offset >= frame.Input.Length)
                throw new InvalidContractException("Bad getcallvalue call");
            return frame.Input[offset];
        }

        public static int Handler_Env_GetCallSize()
        {
            Logger.LogInformation("Handler_Env_GetCallSize()");
            var frame = VirtualMachine.ExecutionFrames.Peek();
            frame.UseGas(GasMetering.GetCallSizeGasCost);
            return frame.Input.Length;
        }

        public static void Handler_Env_CopyCallValue(int from, int to, int offset)
        {
            Logger.LogInformation($"Handler_Env_CopyCallValue({from}, {to}, {offset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call COPYCALLVALUE outside wasm frame");
            if (from < 0 || to > frame.Input.Length || from > to)
                throw new InvalidContractException("Copy to contract memory failed: bad range");
            if (!SafeCopyToMemory(frame.Memory, frame.Input.Skip(from).Take(to - from).ToArray(), offset))
                throw new InvalidContractException("Copy to contract memory failed");
        }

        public static void Handler_Env_WriteLog(int dataOffset, int dataLength, int topicsNum, 
            int topic0Offset, 
            int topic1Offset, 
            int topic2Offset, 
            int topic3Offset 
            )
        {
            Logger.LogInformation($"Handler_Env_WriteLog({dataOffset}, {dataLength})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call WRITELOG outside wasm frame");

            if (frame.InvocationContext.Message?.Type == InvocationType.Static)
            {
                throw new InvalidOperationException("Cannot call WRITELOG in STATICCALL");
            }

            var data = SafeCopyFromMemory(frame.Memory, dataOffset, dataLength);
            if (data == null)
                throw new InvalidContractException("Bad call to WRITELOG,  can't read data");

            if (topicsNum < 1)
                throw new InvalidContractException("Bad call to WRITELOG, we should have at least one topic");

            var topic0 = SafeCopyFromMemory(frame.Memory, topic0Offset, 32);
            if (topic0 == null)
                throw new InvalidContractException("Bad call to WRITELOG,  can't read topic0");

            var eventObj = new Event
            {
                Contract = frame.CurrentAddress,
                Data = ByteString.CopyFrom(data),
                TransactionHash = frame.InvocationContext.Receipt.Hash,
                SignatureHash =  topic0.ToUInt256()
            };
            frame.InvocationContext.Snapshot.Events.AddEvent(eventObj);
        }

        public static int Handler_Env_InvokeContract(
            int callSignatureOffset, int inputLength, int inputOffset, int valueOffset, int gasOffset)
        {
            Logger.LogInformation($"Handler_Env_InvokeContract({callSignatureOffset}, {inputLength}, {inputOffset}, {valueOffset}, {gasOffset})");
            return InvokeContract(callSignatureOffset, inputLength, inputOffset, valueOffset, gasOffset, InvocationType.Regular);
        }

        public static int Handler_Env_InvokeDelegateContract(
            int callSignatureOffset, int inputLength, int inputOffset, int valueOffset, int gasOffset)
        {
            Logger.LogInformation($"Handler_Env_InvokeDelegateContract({callSignatureOffset}, {inputLength}, {inputOffset}, {valueOffset}, {gasOffset})");
            return InvokeContract(callSignatureOffset, inputLength, inputOffset, valueOffset, gasOffset, InvocationType.Delegate);
        }

        public static int Handler_Env_InvokeStaticContract(
            int callSignatureOffset, int inputLength, int inputOffset, int valueOffset, int gasOffset)
        {
            Logger.LogInformation($"Handler_Env_InvokeStaticContract({callSignatureOffset}, {inputLength}, {inputOffset}, {valueOffset}, {gasOffset})");
            return InvokeContract(callSignatureOffset, inputLength, inputOffset, valueOffset, gasOffset, InvocationType.Static);
        }

        public static int Handler_Env_Transfer(
            int callSignatureOffset, int valueOffset)
        {
            Logger.LogInformation($"Handler_Env_Transfer({callSignatureOffset}, {valueOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call transfer outside wasm frame");
            var snapshot = frame.InvocationContext.Snapshot;
            var addressBuffer = SafeCopyFromMemory(frame.Memory, callSignatureOffset, 20);
            if (addressBuffer is null)
                throw new InvalidContractException("Bad call to transfer function");
            var address = addressBuffer.ToUInt160();
            Logger.LogInformation($"Address: {address.ToHex()}");
            var msgValue = SafeCopyFromMemory(frame.Memory, valueOffset, 32)?.ToUInt256();
            var value = msgValue!.ToMoney();
            Logger.LogInformation($"Value: {value}");

            if (value is null)
                throw new InvalidContractException("Bad call to transfer function");
            if (value > Money.Zero)
            {
                frame.UseGas(GasMetering.TransferFundsGasCost);
                var result = snapshot.Balances.TransferBalance(frame.CurrentAddress, address, value);
                if (!result)
                    throw new InsufficientFundsException();
            }

            return 0;
        }

        public static int Handler_Env_Create(int valueOffset, int dataOffset, int dataLength, int resultOffset)
        {
            Logger.LogInformation($"Handler_Env_Create({valueOffset}, {dataOffset}, {dataLength}, {resultOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call Create outside wasm frame");
            if (Hardfork.HardforkHeights.IsHardfork_2Active(frame!.InvocationContext.Snapshot.Blocks.GetTotalBlockHeight()))
                return Handler_Env_Create_V2(valueOffset, dataOffset, dataLength, resultOffset, frame);
            return Handler_Env_Create_V1(valueOffset, dataOffset, dataLength, resultOffset, frame);
        }

        private static int Handler_Env_Create_V1(int valueOffset, int dataOffset, int dataLength, int resultOffset,
            WasmExecutionFrame? frame)
        {
            Logger.LogInformation($"Handler_Env_Create_V1({valueOffset}, {dataOffset}, {dataLength}, {resultOffset})");

            if (frame.InvocationContext.Message?.Type == InvocationType.Static)
            {
                throw new InvalidOperationException("Cannot call Create in STATICCALL");
            }

            var context = frame.InvocationContext;
            var snapshot = context.Snapshot;
            var dataBuffer = SafeCopyFromMemory(frame.Memory, dataOffset, dataLength);
            var value = SafeCopyFromMemory(frame.Memory, valueOffset, 32)?.ToUInt256()!.ToMoney();

            if (value is null)
                throw new InvalidContractException("Bad call to Create function");
            if (snapshot.Balances.GetBalance(frame.CurrentAddress) < value)
            {
                throw new InsufficientFundsException();
            }

            // calculate contract hash and register it
            frame.UseGas(checked(GasMetering.DeployCost + GasMetering.DeployCostPerByte * (ulong) dataBuffer.Length));
            var receipt = context.Receipt ?? throw new InvalidOperationException();

            var hash = UInt160Utils.Zero.ToBytes().Ripemd();
            if (receipt.Transaction?.From != null)
            {
                hash = receipt.Transaction.From.ToBytes()
                    .Concat(receipt.Transaction.Nonce.ToBytes())
                    .Ripemd();
            }

            var contract = new Contract(hash, dataBuffer);

            if (!VirtualMachine.VerifyContract(contract.ByteCode, 
                    Hardfork.HardforkHeights.IsHardfork_2Active(context.Snapshot.Blocks.GetTotalBlockHeight())))
            {
                throw new InvalidContractException("Failed to verify contract");
            }

            try
            {
                snapshot.Contracts.AddContract(context.Sender, contract);
            }
            catch (OutOfGasException e)
            {
                frame.UseGas(e.GasUsed);
                throw;
            }

            // transfer funds
            frame.UseGas(GasMetering.TransferFundsGasCost);
            snapshot.Balances.TransferBalance(frame.CurrentAddress, hash, value);

            SafeCopyToMemory(frame.Memory, hash.ToBytes(), resultOffset);

            return 0;
        }

        private static int Handler_Env_Create_V2(int valueOffset, int dataOffset, int dataLength, int resultOffset,  
            WasmExecutionFrame? frame)
        {
            Logger.LogInformation($"Handler_Env_Create_V2({valueOffset}, {dataOffset}, {dataLength}, {resultOffset})");

            if (frame.InvocationContext.Message?.Type == InvocationType.Static)
            {
                throw new InvalidOperationException("Cannot call Create in STATICCALL");
            }

            var context = frame.InvocationContext;
            var snapshot = context.Snapshot;
            var dataBuffer = SafeCopyFromMemory(frame.Memory, dataOffset, dataLength);
            var msgValue = SafeCopyFromMemory(frame.Memory, valueOffset, 32)?.ToUInt256();
            var value = msgValue!.ToMoney();

            if (value is null)
                throw new InvalidContractException("Bad call to Create function");
            if (snapshot.Balances.GetBalance(frame.CurrentAddress) < value)
            {
                throw new InsufficientFundsException();
            }

            // calculate contract hash and register it
            frame.UseGas(checked(GasMetering.DeployCost + GasMetering.DeployCostPerByte * (ulong) dataBuffer.Length));
            var receipt = context.Receipt ?? throw new InvalidOperationException();

            var hash = UInt160Utils.Zero.ToBytes().Ripemd();
            if (receipt.Transaction?.From != null)
            {
                hash = receipt.Transaction.From.ToBytes()
                    .Concat(receipt.Transaction.Nonce.ToBytes())
                    .Ripemd();
            }

            // deployment code
            var deploymentContract = new Contract(hash, dataBuffer);

            if (!VirtualMachine.VerifyContract(deploymentContract.ByteCode, true))
            {
                throw new InvalidContractException("Failed to verify deployment contract");
            }

            try
            {
                snapshot.Contracts.AddContract(context.Sender, deploymentContract);
            }
            catch (OutOfGasException e)
            {
                frame.UseGas(e.GasUsed);
                throw;
            }

            InvocationMessage invocationMessage = new InvocationMessage {
                Sender = frame.InvocationContext.Message?.Delegate ?? frame.CurrentAddress,
                Value = msgValue,
                Type = InvocationType.Regular,
            };
            var status = DoInternalCall(frame.CurrentAddress, hash, Array.Empty<byte>(), frame.GasLimit, invocationMessage);

            if (status.Status != ExecutionStatus.Ok || status.ReturnValue is null)
            {
                throw new InvalidContractException("Failed to initialize contract");
            }

            // runtime code
            var runtimeContract = new Contract(hash, status.ReturnValue);

            if (!VirtualMachine.VerifyContract(runtimeContract.ByteCode, true))
            {
                throw new InvalidContractException("Failed to verify runtime contract");
            }

            try
            {
                snapshot.Contracts.AddContract(context.Sender, runtimeContract);
            }
            catch (OutOfGasException e)
            {
                frame.UseGas(e.GasUsed);
                throw;
            }

            // transfer funds
            frame.UseGas(GasMetering.TransferFundsGasCost);
            snapshot.Balances.TransferBalance(frame.CurrentAddress, hash, value);

            SafeCopyToMemory(frame.Memory, hash.ToBytes(), resultOffset);

            return 0;
        }

        public static int Handler_Env_Create2(int valueOffset, int dataOffset, int dataLength, int saltOffset, int resultOffset)
        {
            Logger.LogInformation($"Handler_Env_Create2({valueOffset}, {dataOffset}, {dataLength}, {resultOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call Create2 outside wasm frame");
            if (Hardfork.HardforkHeights.IsHardfork_2Active(frame!.InvocationContext.Snapshot.Blocks.GetTotalBlockHeight()))
                return Handler_Env_Create2_V2(valueOffset, dataOffset, dataLength, saltOffset, resultOffset, frame);
            return Handler_Env_Create2_V1(valueOffset, dataOffset, dataLength, saltOffset, resultOffset, frame);
        }

        public static int Handler_Env_Create2_V1(int valueOffset, int dataOffset, int dataLength, int saltOffset, int resultOffset, 
            WasmExecutionFrame? frame)
        {
            Logger.LogInformation($"Handler_Env_Create2_V1({valueOffset}, {dataOffset}, {dataLength}, {saltOffset}, {resultOffset})");

            if (frame.InvocationContext.Message?.Type == InvocationType.Static)
            {
                throw new InvalidOperationException("Cannot call Create2 in STATICCALL");
            }

            var context = frame.InvocationContext;
            var snapshot = context.Snapshot;
            var dataBuffer = SafeCopyFromMemory(frame.Memory, dataOffset, dataLength);
            var value = SafeCopyFromMemory(frame.Memory, valueOffset, 32)?.ToUInt256()!.ToMoney();
            var salt = SafeCopyFromMemory(frame.Memory, saltOffset, 32)?.Reverse();

            if (value is null)
                throw new InvalidContractException("Bad call to Create2 function");
            if (snapshot.Balances.GetBalance(frame.CurrentAddress) < value)
            {
                throw new InsufficientFundsException();
            }

            // calculate contract hash and register it
            frame.UseGas(checked(GasMetering.DeployCost + GasMetering.DeployCostPerByte * (ulong) dataBuffer.Length));
            var receipt = context.Receipt ?? throw new InvalidOperationException();

            var hash = "0xFF".HexToBytes()
                .Concat((frame.InvocationContext.Message?.Delegate ?? frame.CurrentAddress).ToBytes())
                .Concat(salt)
                .Concat(dataBuffer.KeccakBytes())
                .KeccakBytes().Skip(12).Take(20).ToArray().ToUInt160();

            var contract = new Contract(hash, dataBuffer);

            if (!VirtualMachine.VerifyContract(contract.ByteCode,
                Hardfork.HardforkHeights.IsHardfork_2Active(context.Snapshot.Blocks.GetTotalBlockHeight())))
            {
                throw new InvalidContractException("Failed to verify contract");
            }

            try
            {
                snapshot.Contracts.AddContract(context.Sender, contract);
            }
            catch (OutOfGasException e)
            {
                frame.UseGas(e.GasUsed);
                throw;
            }

            // transfer funds
            frame.UseGas(GasMetering.TransferFundsGasCost);
            snapshot.Balances.TransferBalance(frame.CurrentAddress, hash, value);

            SafeCopyToMemory(frame.Memory, hash.ToBytes(), resultOffset);

            return 0;
        }

        public static int Handler_Env_Create2_V2(int valueOffset, int dataOffset, int dataLength, int saltOffset, int resultOffset, 
            WasmExecutionFrame? frame)
        {
            Logger.LogInformation($"Handler_Env_Create2_V2({valueOffset}, {dataOffset}, {dataLength}, {saltOffset}, {resultOffset})");

            if (frame.InvocationContext.Message?.Type == InvocationType.Static)
            {
                throw new InvalidOperationException("Cannot call Create2 in STATICCALL");
            }

            var context = frame.InvocationContext;
            var snapshot = context.Snapshot;
            var dataBuffer = SafeCopyFromMemory(frame.Memory, dataOffset, dataLength);
            var msgValue = SafeCopyFromMemory(frame.Memory, valueOffset, 32)?.ToUInt256();
            var value = msgValue!.ToMoney();
            var salt = SafeCopyFromMemory(frame.Memory, saltOffset, 32)?.Reverse();

            if (value is null)
                throw new InvalidContractException("Bad call to Create2 function");
            if (snapshot.Balances.GetBalance(frame.CurrentAddress) < value)
            {
                throw new InsufficientFundsException();
            }

            // calculate contract hash and register it
            frame.UseGas(checked(GasMetering.DeployCost + GasMetering.DeployCostPerByte * (ulong) dataBuffer.Length));
            var receipt = context.Receipt ?? throw new InvalidOperationException();

            var hash = "0xFF".HexToBytes()
                .Concat((frame.InvocationContext.Message?.Delegate ?? frame.CurrentAddress).ToBytes())
                .Concat(salt)
                .Concat(dataBuffer.KeccakBytes())
                .KeccakBytes().Skip(12).Take(20).ToArray().ToUInt160();

            // deployment code
            var deploymentContract = new Contract(hash, dataBuffer);

            if (!VirtualMachine.VerifyContract(deploymentContract.ByteCode, true))
            {
                throw new InvalidContractException("Failed to verify deployment contract");
            }

            try
            {
                snapshot.Contracts.AddContract(context.Sender, deploymentContract);
            }
            catch (OutOfGasException e)
            {
                frame.UseGas(e.GasUsed);
                throw;
            }

            InvocationMessage invocationMessage = new InvocationMessage {
                Sender = frame.InvocationContext.Message?.Delegate ?? frame.CurrentAddress,
                Value = msgValue,
                Type = InvocationType.Regular,
            };
            var status = DoInternalCall(frame.CurrentAddress, hash, Array.Empty<byte>(), frame.GasLimit, invocationMessage);

            if (status.Status != ExecutionStatus.Ok || status.ReturnValue is null)
            {
                throw new InvalidContractException("Failed to initialize contract");
            }

            // runtime code
            var runtimeContract = new Contract(hash, status.ReturnValue);

            if (!VirtualMachine.VerifyContract(runtimeContract.ByteCode, true))
            {
                throw new InvalidContractException("Failed to verify runtime contract");
            }

            try
            {
                snapshot.Contracts.AddContract(context.Sender, runtimeContract);
            }
            catch (OutOfGasException e)
            {
                frame.UseGas(e.GasUsed);
                throw;
            }

            // transfer funds
            frame.UseGas(GasMetering.TransferFundsGasCost);
            snapshot.Balances.TransferBalance(frame.CurrentAddress, hash, value);

            SafeCopyToMemory(frame.Memory, hash.ToBytes(), resultOffset);

            return 0;
        }

        public static int Handler_Env_GetReturnSize()
        {
            Logger.LogInformation("Handler_Env_GetReturnSize()");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call LOADSTORAGE outside wasm frame");
            frame.UseGas(GasMetering.GetReturnSizeGasCost);
            return frame.LastChildReturnValue.Length;
        }

        public static void Handler_Env_CopyReturnValue(int resultOffset, int dataOffset, int length)
        {
            Logger.LogInformation($"Handler_Env_CopyReturnValue({resultOffset}, {dataOffset}, {length})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GetReturnValue outside wasm frame");
            frame.UseGas(GasMetering.GetReturnValueGasCost);
            if (dataOffset < 0 || length < 0 || dataOffset + length > frame.LastChildReturnValue.Length)
                throw new InvalidContractException("Bad getreturnvalue call");
            var result = new byte[length];
            Array.Copy(frame.LastChildReturnValue, dataOffset, result, 0, length);
            SafeCopyToMemory(frame.Memory, result, resultOffset);
        }

        public static int Handler_Env_GetCodeSize()
        {
            Logger.LogInformation("Handler_Env_GetCodeSize()");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GetCodeSize outside wasm frame");
            frame.UseGas(GasMetering.GetCodeSizeGasCost);
            var byteCode = frame.InvocationContext.Snapshot.Contracts.GetContractByHash(frame.CurrentAddress)?.ByteCode ?? Array.Empty<byte>();
            return byteCode.Length;
        }

        public static void Handler_Env_CopyCodeValue(int resultOffset, int dataOffset, int length)
        {
            Logger.LogInformation($"Handler_Env_CopyCodeValue({resultOffset}, {dataOffset}, {length})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call CopyCodeValue outside wasm frame");
            frame.UseGas(GasMetering.CopyCodeValueGasCost);
            var byteCode = frame.InvocationContext.Snapshot.Contracts.GetContractByHash(frame.CurrentAddress)?.ByteCode ?? Array.Empty<byte>();
            if (dataOffset < 0 || length < 0 || dataOffset + length > byteCode.Length)
                throw new InvalidContractException("Bad CopyCodeValue call");
            var result = new byte[length];
            Array.Copy(byteCode, dataOffset, result, 0, length);
            SafeCopyToMemory(frame.Memory, result, resultOffset);
        }

        public static void Handler_Env_LoadStorage(int keyOffset, int valueOffset)
        {
            Logger.LogInformation($"Handler_Env_LoadStorage({keyOffset}, {valueOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call LOADSTORAGE outside wasm frame");
            frame.UseGas(GasMetering.LoadStorageGasCost);
            var key = SafeCopyFromMemory(frame.Memory, keyOffset, 32);
            if (key is null) throw new InvalidContractException("Bad call to LOADSTORAGE");
            if (key.Length < 32)
                key = _AlignTo32(key);
            var value = frame.InvocationContext.Snapshot.Storage.GetValue(frame.InvocationContext.Message?.Delegate ?? frame.CurrentAddress, key.ToUInt256());
            if (!SafeCopyToMemory(frame.Memory, value.ToBytes(), valueOffset))
                throw new InvalidContractException("Cannot copy storageload result to memory");
        }

        public static void Handler_Env_SaveStorage(int keyOffset, int valueOffset)
        {
            Logger.LogInformation($"Handler_Env_SaveStorage({keyOffset}, {valueOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call SAVESTORAGE outside wasm frame");

            if (frame.InvocationContext.Message?.Type == InvocationType.Static)
            {
                throw new InvalidOperationException("Cannot call SAVESTORAGE in STATICCALL");
            }

            frame.UseGas(GasMetering.SaveStorageGasCost);
            var key = SafeCopyFromMemory(frame.Memory, keyOffset, 32);
            if (key is null)
                throw new InvalidContractException("Bad call to SAVESTORAGE");
            if (key.Length < 32)
                key = _AlignTo32(key);
            var value = SafeCopyFromMemory(frame.Memory, valueOffset, 32);
            if (value is null) throw new InvalidContractException("Bad call to SAVESTORAGE");
            frame.InvocationContext.Snapshot.Storage.SetValue(
                frame.InvocationContext.Message?.Delegate ?? frame.CurrentAddress, key.ToUInt256(), value.ToUInt256()
            );
        }

        public static void Handler_Env_LoadStorageString(int keyOffset, int resultOffset)
        {
            Logger.LogInformation($"Handler_Env_LoadStorageString({keyOffset}, {resultOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call LOADSTORAGESTRING outside wasm frame");
            
            // load key
            var key = SafeCopyFromMemory(frame.Memory, keyOffset, 32);
            if (key is null)
                throw new InvalidContractException("Bad call to LOADSTORAGESTRING");
            if (key.Length < 32)
                key = _AlignTo32(key);

            var storageAddress = frame.InvocationContext.Message?.Delegate ?? frame.CurrentAddress;
            var valueLength = (int) frame.InvocationContext.Snapshot.Storage.GetValue(storageAddress, key.ToUInt256()).ToBigInteger();

            // calculate number of blocks
            var blocks = valueLength / 32 + (valueLength % 32 == 0 ? 0 : 1);
            frame.UseGas(GasMetering.LoadStorageGasCost * (ulong) (blocks + 1));

            // load string from the storage block by block
            var slot = key.ToUInt256().ToBigInteger();
            var value = new byte[valueLength];

            for (int block = 0; block < blocks; block++)
            {
                slot = slot + 1.ToUInt256().ToBigInteger();

                var valueBlock = frame.InvocationContext.Snapshot.Storage.GetValue(storageAddress, slot.ToUInt256()).ToBytes();
                Array.Copy(valueBlock, 0, value, block * 32, Math.Min(32, valueLength - block * 32));
            }

            if (!SafeCopyToMemory(frame.Memory, value, resultOffset))
                throw new InvalidContractException("Cannot copy LOADSTORAGESTRING result to memory");
        }

        public static void Handler_Env_SaveStorageString(int keyOffset, int valueOffset, int valueLength)
        {
            Logger.LogInformation($"Handler_Env_SaveStorageString({keyOffset}, {valueOffset}, {valueLength})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call SAVESTORAGESTRING outside wasm frame");

            if (frame.InvocationContext.Message?.Type == InvocationType.Static)
            {
                throw new InvalidOperationException("Cannot call SAVESTORAGESTRING in STATICCALL");
            }

            // calculate number of blocks
            var blocks = valueLength / 32 + (valueLength % 32 == 0 ? 0 : 1);
            frame.UseGas(GasMetering.SaveStorageGasCost * (ulong) (blocks + 1));
            
            // load key and value
            var key = SafeCopyFromMemory(frame.Memory, keyOffset, 32);
            if (key is null)
                throw new InvalidContractException("Bad call to SAVESTORAGESTRING");
            if (key.Length < 32)
                key = _AlignTo32(key);
            var value = SafeCopyFromMemory(frame.Memory, valueOffset, valueLength);
            if (value is null) throw new InvalidContractException("Bad call to SAVESTORAGESTRING");
            
            // save string to the storage block by block
            var slot = key.ToUInt256().ToBigInteger();
            var storageAddress = frame.InvocationContext.Message?.Delegate ?? frame.CurrentAddress;

            frame.InvocationContext.Snapshot.Storage.SetValue(
                storageAddress, slot.ToUInt256(), valueLength.ToUInt256()
            );

            for (int block = 0; block < blocks; block++)
            {
                slot = slot + 1.ToUInt256().ToBigInteger();

                frame.InvocationContext.Snapshot.Storage.SetValue(
                    storageAddress, slot.ToUInt256(), value.Skip(block * 32).Take(32).ToArray().ToUInt256(true)
                );
            }
        }

        public static int Handler_Env_GetStorageStringSize(int keyOffset)
        {
            Logger.LogInformation($"Handler_Env_GetStorageStringSize({keyOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GETSTORAGESTRINGSIZE outside wasm frame");
            frame.UseGas(GasMetering.GetReturnSizeGasCost);
            
            var key = SafeCopyFromMemory(frame.Memory, keyOffset, 32);
            if (key is null)
                throw new InvalidContractException("Bad call to GETSTORAGESTRINGSIZE");
            if (key.Length < 32)
                key = _AlignTo32(key);

            var valueLength = (int) frame.InvocationContext.Snapshot.Storage.GetValue(frame.InvocationContext.Message?.Delegate ?? frame.CurrentAddress, key.ToUInt256()).ToBigInteger();

            return valueLength;
        }

        private static byte[] _AlignTo32(byte[] buffer)
        {
            if (buffer.Length == 32)
                return buffer;
            var result = new byte[32];
            Array.Copy(buffer, 0, result, 0, buffer.Length);
            return result;
        }

        public static void Handler_Env_SetReturn(int offset, int length)
        {
            Logger.LogInformation($"Handler_Env_SetReturn({offset}, {length})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call SETRETURN outside wasm frame");
            var ret = SafeCopyFromMemory(frame.Memory, offset, length);
            Logger.LogInformation($"ret: {ret.ToHex()}");
            if (ret is null)
                throw new InvalidContractException("Bad call to SETRETURN");
            frame.ReturnValue = ret;
        }

        public static void Handler_Env_GetSender(int dataOffset)
        {
            Logger.LogInformation($"Handler_Env_GetSender({dataOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GETSENDER outside wasm frame");
            var data = (frame.InvocationContext.Message?.Sender ?? frame.InvocationContext.Sender).ToBytes();
            Logger.LogInformation($"Data: {data.ToHex()}");
            var ret = SafeCopyToMemory(frame.Memory, data, dataOffset);
            if (!ret)
                throw new InvalidContractException("Bad call to GETSENDER");
        }

        public static void Handler_Env_GetGasLeft(int dataOffset)
        {
            Logger.LogInformation($"Handler_Env_GetGasLeft({dataOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GETGASLEFT outside wasm frame");
            var data = (frame.GasLimit - frame.GasUsed).ToBytes().ToArray();
            var ret = SafeCopyToMemory(frame.Memory, data, dataOffset);
            if (!ret)
                throw new InvalidContractException("Bad call to (get_gas_left)");
        }

        public static void Handler_Env_GetTxOrigin(int dataOffset)
        {
            Logger.LogInformation($"Handler_Env_GetTxOrigin({dataOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GETTXORIGIN outside wasm frame");
            var data = frame.InvocationContext.Sender.ToBytes();
            var ret = SafeCopyToMemory(frame.Memory, data, dataOffset);
            if (!ret)
                throw new InvalidContractException("Bad call to (get_tx_origin)");
        }

        public static void Handler_Env_GetTxGasPrice(int dataOffset)
        {
            Logger.LogInformation($"Handler_Env_GetTxGasPrice({dataOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GETTXGASPRICE outside wasm frame");
            var data = frame.InvocationContext.Receipt.Transaction.GasPrice.ToBytes().ToArray();
            var ret = SafeCopyToMemory(frame.Memory, data, dataOffset);
            if (!ret)
                throw new InvalidContractException("Bad call to (get_tx_gas_price)");
        }

        public static void Handler_Env_GetBlockNumber(int dataOffset)
        {
            Logger.LogInformation($"Handler_Env_GetBlockNumber({dataOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GETBLOCKNUMBER outside wasm frame");
            var data = frame.InvocationContext.Snapshot.Blocks.GetTotalBlockHeight().ToBytes().ToArray();
            var ret = SafeCopyToMemory(frame.Memory, data, dataOffset);
            if (!ret)
                throw new InvalidContractException("Bad call to (get_block_number)");
        }

        public static void Handler_Env_SystemHalt(int haltCode)
        {
            Logger.LogInformation($"Handler_Env_SystemHalt({haltCode})"); 
            throw new HaltException(haltCode);
        }

        public static void Handler_Env_CryptoKeccak256(int dataOffset, int dataLength, int resultOffset)
        {
            Logger.LogInformation($"Handler_Env_CryptoKeccak256({dataOffset}, {dataLength}, {resultOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call KECCAK256 outside wasm frame");
            frame.UseGas(GasMetering.Keccak256GasCost + GasMetering.Keccak256GasPerByte * (ulong) dataLength);
            var data = SafeCopyFromMemory(frame.Memory, dataOffset, dataLength) ??
                       throw new InvalidOperationException();
            var result = data.KeccakBytes();
            SafeCopyToMemory(frame.Memory, result, resultOffset);
        }

        public static void Handler_Env_CryptoSha256(int dataOffset, int dataLength, int resultOffset)
        {
            Logger.LogInformation($"Handler_Env_CryptoSha256({dataOffset}, {dataLength}, {resultOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call SHA256 outside wasm frame");
            frame.UseGas(GasMetering.Sha256GasGasCost + GasMetering.Sha256GasPerByte * (ulong) dataLength);
            var data = SafeCopyFromMemory(frame.Memory, dataOffset, dataLength) ??
                       throw new InvalidOperationException();
            var result = data.Sha256Bytes();
            SafeCopyToMemory(frame.Memory, result, resultOffset);
        }

        public static void Handler_Env_CryptoRipemd160(int dataOffset, int dataLength, int resultOffset)
        {
            Logger.LogInformation($"Handler_Env_CryptoRipemd160({dataOffset}, {dataLength}, {resultOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call RIPEMD outside wasm frame");
            frame.UseGas(GasMetering.Ripemd160GasCost + GasMetering.Ripemd160GasPerByte * (ulong) dataLength);
            var data = SafeCopyFromMemory(frame.Memory, dataOffset, dataLength) ??
                       throw new InvalidOperationException();
            var result = data.RipemdBytes();
            SafeCopyToMemory(frame.Memory, result, resultOffset);
        }

        public static void Handler_Env_CryptoRecover(int hashOffset, int v, int rOffset, int sOffset,
            int resultOffset)
        {
            Logger.LogInformation($"Handler_Env_CryptoRecover({hashOffset}, {v}, {rOffset}, {sOffset}, {resultOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call ECRECOVER outside wasm frame");
            frame.UseGas(GasMetering.RecoverGasCost);
            var hash = SafeCopyFromMemory(frame.Memory, hashOffset, 32) ??
                          throw new InvalidOperationException();
            var sig = new byte[SignatureUtils.Length];
            var r = SafeCopyFromMemory(frame.Memory, rOffset, 32) ??
                      throw new InvalidOperationException();
            Array.Copy(r, 0, sig, 0, r.Length);
            var s = SafeCopyFromMemory(frame.Memory, sOffset, 32) ??
                      throw new InvalidOperationException();
            Array.Copy(s, 0, sig, r.Length, s.Length);
            sig[64] = (byte) v;
            var publicKey = VirtualMachine.Crypto.RecoverSignatureHashed(hash, sig);
            var address = VirtualMachine.Crypto.ComputeAddress(publicKey);
            SafeCopyToMemory(frame.Memory, address, resultOffset);
        }

        public static void Handler_Env_CryptoVerify(int messageOffset, int messageLength, int signatureOffset,
            int publicKeyOffset, int resultOffset)
        {
            Logger.LogInformation($"Handler_Env_CryptoRecover({messageOffset}, {messageLength}, {signatureOffset}, {publicKeyOffset}, {resultOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call ECVERIFY outside wasm frame");
            frame.UseGas(GasMetering.VerifyGasCost);
            var message = SafeCopyFromMemory(frame.Memory, messageOffset, messageLength) ??
                          throw new InvalidOperationException();
            var sig = SafeCopyFromMemory(frame.Memory, signatureOffset, SignatureUtils.Length) ??
                      throw new InvalidOperationException();
            var publicKey = SafeCopyFromMemory(frame.Memory, publicKeyOffset, CryptoUtils.PublicKeyLength) ??
                            throw new InvalidOperationException();
            var result = VirtualMachine.Crypto.VerifySignature(message, sig, publicKey);
            SafeCopyToMemory(frame.Memory, new[] {result ? (byte) 1 : (byte) 0}, resultOffset);
        }

        public static void Handler_Env_GetTransferredFunds(int dataOffset)
        {
            Logger.LogInformation($"Handler_Env_GetTransferredFunds({dataOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GETCALLVALUE outside wasm frame");
            var data = frame.InvocationContext.Value.ToBytes();
            var ret = SafeCopyToMemory(frame.Memory, data, dataOffset);
            if (!ret)
                throw new InvalidContractException("Bad call to (get_transferred_funds)");
        }

        public static void Handler_Env_GetTransactionHash(int dataOffset)
        {
            Logger.LogInformation($"Handler_Env_GetTransactionHash({dataOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call TXHASH outside wasm frame");
            var data = frame.InvocationContext.TransactionHash.ToBytes();
            var ret = SafeCopyToMemory(frame.Memory, data, dataOffset);
            if (!ret)
                throw new InvalidContractException("Bad call to (get_transaction_hash)");
        }

        public static void Handle_Env_WriteEvent(int signatureOffset, int valueOffset, int valueLength)
        {
            Logger.LogInformation($"Handle_Env_WriteEvent({signatureOffset}, {valueOffset}, {valueLength})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call WRITEEVENT outside wasm frame");
            frame.UseGas(GasMetering.WriteEventPerByteGas * (uint) (valueLength + 32));
            var signature = SafeCopyFromMemory(frame.Memory, signatureOffset, 32) ??
                            throw new InvalidOperationException();
            var value = SafeCopyFromMemory(frame.Memory, valueOffset, valueLength) ??
                        throw new InvalidOperationException();
            var ev = new Event
            {
                Contract = frame.CurrentAddress,
                Data = ByteString.CopyFrom(value),
                TransactionHash = frame.InvocationContext.TransactionHash,
                Index = 0, /* will be replaced in (IEventSnapshot::AddEvent) method */
                SignatureHash = signature.ToUInt256()
            };
            frame.InvocationContext.Snapshot.Events.AddEvent(ev);
        }
        
        public static void Handler_Env_GetAddress(int resultOffset)
        {
            Logger.LogInformation($"Handler_Env_GetAddress({resultOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GetAddress outside wasm frame");
            var result = (frame.InvocationContext.Message?.Delegate ?? frame.CurrentAddress).ToBytes();
            SafeCopyToMemory(frame.Memory, result, resultOffset);
        }
        
        public static void Handler_Env_GetMsgValue(int dataOffset)
        {
            Logger.LogInformation($"Handler_Env_GetMsgValue({dataOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GetMsgValue outside wasm frame");
            var data = (frame.InvocationContext.Message?.Value ?? frame.InvocationContext.Value).ToBytes().Reverse().ToArray();
            var ret = SafeCopyToMemory(frame.Memory, data, dataOffset);
            if (!ret)
                throw new InvalidContractException("Bad call to (get_msgvalue)");
        }

        public static void Handler_Env_GetBlockGasLimit(int dataOffset)
        {
            Logger.LogInformation($"Handler_Env_GetBlockGasLimit({dataOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GetBlockGasLimit outside wasm frame");
            const ulong defaultBlockGasLimit = GasMetering.DefaultBlockGasLimit;
            
            // Load `default block gasLimit` at given memory offset
            var ret = SafeCopyToMemory(frame.Memory, defaultBlockGasLimit.ToBytes().ToArray(), dataOffset);
            if (!ret)
                throw new InvalidContractException("Bad call to (get_block_gas_limit)");
        }
        
        public static void Handler_Env_GetBlockCoinbase(int dataOffset)
        {
            Logger.LogInformation($"Handler_Env_GetBlockCoinbase({dataOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GetBlockCoinbase outside wasm frame");
            UInt160 coinbase = UInt160Utils.Zero;
            
            // Load `zero address` at given memory offset
            var ret = SafeCopyToMemory(frame.Memory, coinbase.ToBytes().ToArray(), dataOffset);
            if (!ret)
                throw new InvalidContractException("Bad call to (get_block_coinbase_address)");
        }
        
        public static void Handler_Env_GetBlockDifficulty(int dataOffset)
        {
            Logger.LogInformation($"Handler_Env_GetBlockDifficulty({dataOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GetBlockDifficulty outside wasm frame");
            var difficulty = 0;
            
            // Load `zero difficulty` at given memory offset
            var ret = SafeCopyToMemory(frame.Memory, difficulty.ToBytes().ToArray(), dataOffset);
            if (!ret)
                throw new InvalidContractException("Bad call to (get_block_difficulty)");
        }

        public static void Handler_Env_GetExternalBalance(int addressOffset, int resultOffset)
        {
            Logger.LogInformation($"Handler_Env_GetExternalBalance({addressOffset}, {resultOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GetExternalBalance outside wasm frame");
            
            // Get the address from the given memory offset
            var snapshot = frame.InvocationContext.Snapshot;
            var addressBuffer = SafeCopyFromMemory(frame.Memory, addressOffset, 20);
            if (addressBuffer is null)
                throw new InvalidContractException("Bad call to (get_external_balance)");
            var address = addressBuffer.Take(20).ToArray().ToUInt160();
            
            // Get balance at the given addres
            var balance = snapshot.Balances.GetBalance(address);
            
            // Load balance at the given resultOffset
            var result = SafeCopyToMemory(frame.Memory, balance.ToUInt256().ToBytes().Reverse().ToArray(), resultOffset);

            if (!result)
                throw new InvalidContractException("Bad call to (get_external_balance)");
        }

        public static void Handler_Env_GetExtcodesize(int addressOffset, int resultOffset)
        {
            Logger.LogInformation($"Handler_Env_GetExtcodesize({addressOffset}, {resultOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GetExtcodesize outside wasm frame");
            
            // Get the address from the given memory offset
            var snapshot = frame.InvocationContext.Snapshot;
            var addressBuffer = SafeCopyFromMemory(frame.Memory, addressOffset, 20);
            if (addressBuffer is null)
                throw new InvalidContractException("Bad call to (get_extcodesize)");
            var address = addressBuffer.Take(20).ToArray().ToUInt160();
            
            // Get contract at the given address
            var contract = snapshot.Contracts.GetContractByHash(address);
            
            // Load contract size at the given resultOffset
            var result = SafeCopyToMemory(frame.Memory, (contract?.ByteCode.Length ?? 0).ToBytes().ToArray(), resultOffset);

            if (!result)
                throw new InvalidContractException("Bad call to (get_extcodesize)");
        }
        
        public static void Handler_Env_GetBlockTimestamp(int dataOffset)
        {
            Logger.LogInformation($"Handler_Env_GetBlockTimestamp()");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GetBlockTimestamp outside wasm frame");

            // Get the TotalBlockHeight at the given Snapshot
            var snapshot = frame.InvocationContext.Snapshot;
            var blockHeight = snapshot.Blocks.GetTotalBlockHeight();
            
            var timeStamp = blockHeight;
            
            // Load timestamp at the given dataOffset
            var result = SafeCopyToMemory(frame.Memory, timeStamp.ToBytes().ToArray(), dataOffset);
            if (!result)
                throw new InvalidContractException("Bad call to (get_block_timestamp)");
        }

        public static void Handler_Env_GetBlockHash(int numberOffset, int dataOffset)
        {
            Logger.LogInformation($"Handler_Env_GetBlockHash({numberOffset}, {dataOffset})");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call Handler_Env_GetBlockHash outside wasm frame");

            var snapshot = frame.InvocationContext.Snapshot;
            var blockNumberBuffer = SafeCopyFromMemory(frame.Memory, numberOffset, 8);
            if (blockNumberBuffer is null)
                throw new InvalidContractException("Bad call to (get_block_hash)");
            var blockNumber = BitConverter.ToUInt64(blockNumberBuffer, 0);

            // Get block at the given height
            var block = snapshot.Blocks.GetBlockByHeight(blockNumber);
            
            // Get block's hash
            if (block is null)
                throw new InvalidContractException("Bad call to (get_block_hash)");
            var hash = block.Hash;
            
            // Load hash at the given dataOffset
            var result = SafeCopyToMemory(frame.Memory, hash.ToBytes().ToArray(), dataOffset);
            if (!result)
                throw new InvalidContractException("Bad call to (get_block_hash)");
        }
        
        public static void Handler_Env_GetChainId(int dataOffset)
        {
            Logger.LogInformation($"Handler_Env_GetChainId()");
            var frame = VirtualMachine.ExecutionFrames.Peek() as WasmExecutionFrame
                        ?? throw new InvalidOperationException("Cannot call GetChainId outside wasm frame");

            var chainId = TransactionUtils.ChainId;
            
            // Load chainId at the given dataOffset
            var result = SafeCopyToMemory(frame.Memory, chainId.ToBytes().ToArray(), dataOffset);
            if (!result)
                throw new InvalidContractException("Bad call to (get_chain_id)");
        }

        private static FunctionImport CreateImport(string methodName)
        {
            var methodInfo = typeof(ExternalHandler).GetMethod(methodName) ?? throw new ArgumentNullException();
            var isAction = methodInfo.ReturnType == typeof(void);
            var types = methodInfo.GetParameters().Select(p => p.ParameterType);

            if (isAction)
            {
                return new FunctionImport(
                    Delegate.CreateDelegate(Expression.GetActionType(types.ToArray()), methodInfo)
                );
            }

            types = types.Concat(new[] {methodInfo.ReturnType});
            return new FunctionImport(Delegate.CreateDelegate(Expression.GetFuncType(types.ToArray()), methodInfo));
        }

        public ImportDictionary GetFunctionImports()
        {
            var result = new ImportDictionary
            {
                {EnvModule, "get_call_value", CreateImport(nameof(Handler_Env_GetCallValue))},
                {EnvModule, "get_call_size", CreateImport(nameof(Handler_Env_GetCallSize))},
                {EnvModule, "copy_call_value", CreateImport(nameof(Handler_Env_CopyCallValue))},
                {EnvModule, "invoke_contract", CreateImport(nameof(Handler_Env_InvokeContract))},
                {EnvModule, "invoke_delegate_contract", CreateImport(nameof(Handler_Env_InvokeDelegateContract))},
                {EnvModule, "invoke_static_contract", CreateImport(nameof(Handler_Env_InvokeStaticContract))},
                {EnvModule, "transfer", CreateImport(nameof(Handler_Env_Transfer))},
                {EnvModule, "create", CreateImport(nameof(Handler_Env_Create))},
                {EnvModule, "create2", CreateImport(nameof(Handler_Env_Create2))},
                {EnvModule, "get_return_size", CreateImport(nameof(Handler_Env_GetReturnSize))},
                {EnvModule, "copy_return_value", CreateImport(nameof(Handler_Env_CopyReturnValue))},
                {EnvModule, "get_code_size", CreateImport(nameof(Handler_Env_GetCodeSize))},
                {EnvModule, "copy_code_value", CreateImport(nameof(Handler_Env_CopyCodeValue))},
                {EnvModule, "write_log", CreateImport(nameof(Handler_Env_WriteLog))},
                {EnvModule, "load_storage", CreateImport(nameof(Handler_Env_LoadStorage))},
                {EnvModule, "save_storage", CreateImport(nameof(Handler_Env_SaveStorage))},
                {EnvModule, "load_storage_string", CreateImport(nameof(Handler_Env_LoadStorageString))},
                {EnvModule, "save_storage_string", CreateImport(nameof(Handler_Env_SaveStorageString))},
                {EnvModule, "get_storage_string_size", CreateImport(nameof(Handler_Env_GetStorageStringSize))},
                {EnvModule, "set_return", CreateImport(nameof(Handler_Env_SetReturn))},
                {EnvModule, "get_sender", CreateImport(nameof(Handler_Env_GetSender))},
                {EnvModule, "get_gas_left", CreateImport(nameof(Handler_Env_GetGasLeft))},
                {EnvModule, "get_tx_origin", CreateImport(nameof(Handler_Env_GetTxOrigin))},
                {EnvModule, "get_tx_gas_price", CreateImport(nameof(Handler_Env_GetTxGasPrice))},
                {EnvModule, "get_block_number", CreateImport(nameof(Handler_Env_GetBlockNumber))},
                {EnvModule, "get_block_hash", CreateImport(nameof(Handler_Env_GetBlockHash))},
                {EnvModule, "system_halt", CreateImport(nameof(Handler_Env_SystemHalt))},
                {EnvModule, "get_transferred_funds", CreateImport(nameof(Handler_Env_GetTransferredFunds))},
                {EnvModule, "get_transaction_hash", CreateImport(nameof(Handler_Env_GetTransactionHash))},
                {EnvModule, "write_event", CreateImport(nameof(Handle_Env_WriteEvent))},
                {EnvModule, "get_address", CreateImport(nameof(Handler_Env_GetAddress))},
                {EnvModule, "get_msgvalue", CreateImport(nameof(Handler_Env_GetMsgValue))},
                {EnvModule, "get_block_gas_limit", CreateImport(nameof(Handler_Env_GetBlockGasLimit))},
                {EnvModule, "get_block_coinbase_address", CreateImport(nameof(Handler_Env_GetBlockCoinbase))},
                {EnvModule, "get_block_difficulty", CreateImport(nameof(Handler_Env_GetBlockDifficulty))},
                {EnvModule, "get_external_balance", CreateImport(nameof(Handler_Env_GetExternalBalance))},
                {EnvModule, "get_extcodesize", CreateImport(nameof(Handler_Env_GetExtcodesize))},
                {EnvModule, "get_block_timestamp", CreateImport(nameof(Handler_Env_GetBlockTimestamp))},
                {EnvModule, "get_chain_id", CreateImport(nameof(Handler_Env_GetChainId))},
                // /* crypto hash bindings */
                {EnvModule, "crypto_keccak256", CreateImport(nameof(Handler_Env_CryptoKeccak256))},
                {EnvModule, "crypto_sha256", CreateImport(nameof(Handler_Env_CryptoSha256))},
                {EnvModule, "crypto_ripemd160", CreateImport(nameof(Handler_Env_CryptoRipemd160))},
                // /* cryptography methods */
                {EnvModule, "crypto_recover", CreateImport(nameof(Handler_Env_CryptoRecover))},
                {EnvModule, "crypto_verify", CreateImport(nameof(Handler_Env_CryptoVerify))},
                /* memory methods */
            };
            return result;
        }
    }
}