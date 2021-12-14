using System;
using System.Linq;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Blockchain.VM.ExecutionFrame;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class DeployContract : ISystemContract
    {
        private readonly InvocationContext _context;

        private static readonly ILogger<DeployContract> Logger = LoggerFactory.GetLoggerForClass<DeployContract>();

        public DeployContract(InvocationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public ContractStandard ContractStandard => ContractStandard.DeployContract;

        [ContractMethod(DeployInterface.MethodDeploy)]
        public ExecutionStatus Deploy(byte[] byteCode, SystemContractExecutionFrame frame)
        {
            if (frame.InvocationContext.Snapshot.Blocks.GetTotalBlockHeight() < HardforkHeights.Hardfork_2)
                return DeployV1(byteCode, frame);
            return DeployV2(byteCode, frame);
        }

        private ExecutionStatus DeployV1(byte[] byteCode, SystemContractExecutionFrame frame)
        {
            Logger.LogInformation($"Deploy({byteCode.ToHex()})");
            frame.ReturnValue = Array.Empty<byte>();
            frame.UseGas(checked(GasMetering.DeployCost + GasMetering.DeployCostPerByte * (ulong) byteCode.Length));
            var receipt = _context.Receipt ?? throw new InvalidOperationException();
            /* calculate contract hash and register it */
            var hash = UInt160Utils.Zero.ToBytes().Ripemd();
            if (receipt.Transaction?.From != null)
            {
                hash = receipt.Transaction.From.ToBytes()
                    .Concat(receipt.Transaction.Nonce.ToBytes())
                    .Ripemd();
            }
            
            var contract = new Contract(hash, byteCode);

            if (!VirtualMachine.VerifyContract(contract.ByteCode))
            {
                Logger.LogInformation("Failed to verify contract");
                return ExecutionStatus.ExecutionHalted;
            }

            try
            {
                _context.Snapshot.Contracts.AddContract(_context.Sender, new Contract(hash, contract.ByteCode));
                Logger.LogInformation($"New contract with address {hash.ToHex()} deployed");
            }
            catch (OutOfGasException e)
            {
                Logger.LogInformation("Out of gas");
                frame.UseGas(e.GasUsed);
                return ExecutionStatus.GasOverflow;
            }

            return ExecutionStatus.Ok;
        }
        
        private ExecutionStatus DeployV2(byte[] byteCode, SystemContractExecutionFrame frame)
        {
            Logger.LogInformation($"Deploy({byteCode.ToHex()})");
            frame.ReturnValue = Array.Empty<byte>();
            frame.UseGas(checked(GasMetering.DeployCost + GasMetering.DeployCostPerByte * (ulong) byteCode.Length));
            var receipt = _context.Receipt ?? throw new InvalidOperationException();
            /* calculate contract hash and register it */
            var hash = UInt160Utils.Zero.ToBytes().Ripemd();
            if (receipt.Transaction?.From != null)
            {
                hash = receipt.Transaction.From.ToBytes()
                    .Concat(receipt.Transaction.Nonce.ToBytes())
                    .Ripemd();
            }

            // TODO: this is fake, we have to think of what happens if someone tries to get current address during deploy
            // deployment code
            var deploymentContract = new Contract(hash, byteCode);

            if (!VirtualMachine.VerifyContract(deploymentContract.ByteCode))
            {
                Logger.LogInformation("Failed to verify deployment contract");
                return ExecutionStatus.ExecutionHalted;
            }

            try
            {
                _context.Snapshot.Contracts.AddContract(_context.Sender, new Contract(hash, deploymentContract.ByteCode));
            }
            catch (OutOfGasException e)
            {
                Logger.LogInformation("Out of gas");
                frame.UseGas(e.GasUsed);
                return ExecutionStatus.GasOverflow;
            }

            var status = VirtualMachine.InvokeWasmContract(deploymentContract, _context, Array.Empty<byte>(), frame.GasLimit);

            if (status.Status != ExecutionStatus.Ok || status.ReturnValue is null)
            {
                Logger.LogInformation("Failed to initialize contract");
                return ExecutionStatus.ExecutionHalted;
            }

            // runtime code
            var runtimeContract = new Contract(hash, status.ReturnValue);

            if (!VirtualMachine.VerifyContract(runtimeContract.ByteCode))
            {
                Logger.LogInformation("Failed to verify runtime contract");
                return ExecutionStatus.ExecutionHalted;
            }

            try
            {
                _context.Snapshot.Contracts.AddContract(_context.Sender, new Contract(hash, runtimeContract.ByteCode));
                Logger.LogInformation($"New contract with address {hash.ToHex()} deployed");
            }
            catch (OutOfGasException e)
            {
                Logger.LogInformation("Out of gas");
                frame.UseGas(e.GasUsed);
                return ExecutionStatus.GasOverflow;
            }

            return ExecutionStatus.Ok;
        }
    }
}