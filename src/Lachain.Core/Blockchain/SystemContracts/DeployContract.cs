using System;
using System.Linq;
using Google.Protobuf;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.ContractManager.Attributes;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.VM;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.SystemContracts
{
    public class DeployContract : ISystemContract
    {
        private readonly ContractContext _contractContext;
        private readonly IVirtualMachine _virtualMachine;

        private static readonly ILogger<DeployContract> Logger = LoggerFactory.GetLoggerForClass<DeployContract>();

        public DeployContract(ContractContext contractContext, IVirtualMachine virtualMachine)
        {
            _contractContext = contractContext ?? throw new ArgumentNullException(nameof(contractContext));
            _virtualMachine = virtualMachine;
        }

        public ContractStandard ContractStandard => ContractStandard.DeployContract;

        [ContractMethod(DeployInterface.MethodDeploy)]
        public void Deploy(byte[] byteCode)
        {
            var receipt = _contractContext.Receipt;
            /* calculate contract hash and register it */
            var hash = receipt.Transaction.From.ToBytes()
                .Concat(BitConverter.GetBytes((uint) receipt.Transaction.Nonce))
                .Ripemd();

            var contract = new Contract
            {
                // TODO: this is fake, we have to think of what happens if someone tries to get current address during deploy
                ContractAddress = hash,
                ByteCode = ByteString.CopyFrom(byteCode)
            };

            if (!_virtualMachine.VerifyContract(contract.ByteCode.ToByteArray()))
                throw new InvalidContractException();

            try
            {
                var result = _virtualMachine.InvokeContract(
                    contract,
                    new InvocationContext(_contractContext.Sender, _contractContext.Receipt),
                    Array.Empty<byte>(),
                    _contractContext.GasRemaining
                );
                if (result.Status != ExecutionStatus.Ok || result.ReturnValue is null)
                    throw new InvalidContractException();
                
                _contractContext.Snapshot.Contracts.AddContract(_contractContext.Sender, new Contract
                {
                    ByteCode = ByteString.CopyFrom(result.ReturnValue),
                    ContractAddress = hash
                });
                // TODO: charge gas for this
            }
            catch (OutOfGasException e)
            {
                receipt.GasUsed += e.GasUsed;
                throw new InvalidContractException();
            }
        }
    }
}