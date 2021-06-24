using System;
using System.Linq;
using Lachain.Core.Blockchain.Error;
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
            Logger.LogInformation($"Deploy({byteCode.ToHex()})");
            frame.ReturnValue = new byte[] { };
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
            var contract = new Contract(hash, byteCode);

            if (!VirtualMachine.VerifyContract(contract.ByteCode))
            {
                Logger.LogInformation("Failed to verify contract");
                return ExecutionStatus.ExecutionHalted;
            }

            try
            {
                // TODO: Deploy raw bytecode for now. Need to uncomment when we get convenient tool to get deploy code
                // var result = _virtualMachine.InvokeContract(
                //     contract,
                //     new InvocationContext(_contractContext.Sender, _contractContext.Receipt),
                //     Array.Empty<byte>(),
                //     _contractContext.GasRemaining
                // );
                // if (result.Status != ExecutionStatus.Ok || result.ReturnValue is null)
                //     return ExecutionStatus.ExecutionHalted;

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
    }
}