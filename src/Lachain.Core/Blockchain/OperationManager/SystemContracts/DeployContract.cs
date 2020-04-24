using System;
using System.Linq;
using Google.Protobuf;
using Lachain.Core.Blockchain.ContractManager;
using Lachain.Core.Blockchain.ContractManager.Attributes;
using Lachain.Core.Blockchain.ContractManager.Standards;
using Lachain.Core.VM;
using Lachain.Crypto;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.OperationManager.SystemContracts
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
                ContractAddress = hash,
                ByteCode = ByteString.CopyFrom(byteCode)
            };

            if (!_virtualMachine.VerifyContract(contract.ByteCode.ToByteArray()))
                throw new InvalidContractException();
            
            _contractContext.Snapshot.Contracts.AddContract(receipt.Transaction.From, contract);
            if (_InvokeConstructor(contract, receipt) != OperatingError.Ok)
                throw new InvalidContractException();
            // TODO: change this to eth-like deploy
        }

        private OperatingError _InvokeConstructor(Contract contract, TransactionReceipt receipt)
        {
            try
            {
                var result = _virtualMachine.InvokeContract(
                    contract,
                    new InvocationContext(receipt.Transaction.From, receipt),
                    new byte[] { },
                    receipt.Transaction.GasLimit - receipt.GasUsed
                );
                return result.Status != ExecutionStatus.Ok ? OperatingError.ContractFailed : OperatingError.Ok;
            }
            catch (OutOfGasException e)
            {
                receipt.GasUsed += e.GasUsed;
            }

            return OperatingError.OutOfGas;
        }
    }
}